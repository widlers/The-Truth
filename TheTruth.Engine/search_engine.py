import os
import sys
import json
import requests
from dotenv import load_dotenv
from duckduckgo_search import DDGS
from bs4 import BeautifulSoup
import re
from requests_toolbelt import MultipartEncoder
import c2pa
from PIL import Image, ExifTags

# Load environment variables
load_dotenv()

def get_metadata(image_path: str) -> dict:
    """
    Extracts C2PA and EXIF metadata from an image.
    """
    metadata = {
        "c2pa": None,
        "exif": {},
        "ai_traces": []
    }

    # 1. C2PA (Content Credentials)
    try:
        # Use c2pa-python Reader
        try:
            reader = c2pa.Reader(image_path)
            manifest_json = reader.json()
        except Exception:
             # c2pa.Reader might raise if no manifest found or format error
             manifest_json = None

        if manifest_json:
            import json
            if isinstance(manifest_json, str):
                metadata["c2pa"] = json.loads(manifest_json)
            else:
                metadata["c2pa"] = manifest_json
            
            # Simple AI check in C2PA
            c2pa_str = json.dumps(metadata["c2pa"]).lower()
            if "firefly" in c2pa_str: metadata["ai_traces"].append("C2PA: Adobe Firefly")
            if "dall-e" in c2pa_str: metadata["ai_traces"].append("C2PA: DALL-E")
            if "midjourney" in c2pa_str: metadata["ai_traces"].append("C2PA: Midjourney")
            if "veo" in c2pa_str: metadata["ai_traces"].append("C2PA: Veo (Google)")
            if "synthetic" in c2pa_str: metadata["ai_traces"].append("C2PA: Synthetic Content Identified")
            
    except Exception as e:
        metadata["c2pa_error"] = str(e)

    # 2. EXIF Data via Pillow
    try:
        img = Image.open(image_path)
        exif_data = img.getexif()
        if exif_data:
            for tag_id, value in exif_data.items():
                tag = ExifTags.TAGS.get(tag_id, tag_id)
                
                # Decode bytes to string if possible
                if isinstance(value, bytes):
                    try:
                        value = value.decode()
                    except:
                        value = str(value)
                
                metadata["exif"][str(tag)] = str(value)

                # AI Traces in EXIF
                val_str = str(value).lower()
                if "midjourney" in val_str: metadata["ai_traces"].append(f"EXIF {tag}: Midjourney")
                if "stable diffusion" in val_str: metadata["ai_traces"].append(f"EXIF {tag}: Stable Diffusion")
                if "comfyui" in val_str: metadata["ai_traces"].append(f"EXIF {tag}: ComfyUI")
                if "veo" in val_str: metadata["ai_traces"].append(f"EXIF {tag}: Veo")
                
    except Exception as e:
        metadata["exif_error"] = str(e)

    return metadata

def search_nyt(query, api_key, max_results=3):
    """
    Searches New York Times Article Search API.
    """
    url = "https://api.nytimes.com/svc/search/v2/articlesearch.json"
    params = {
        "q": query,
        "api-key": api_key,
        "sort": "relevance"
    }
    results = []
    try:
        response = requests.get(url, params=params, timeout=10)
        if response.status_code == 200:
            data = response.json()
            docs = data.get('response', {}).get('docs', [])
            for doc in docs[:max_results]:
                results.append({
                    "title": "[NYT] " + doc.get('headline', {}).get('main', 'No Title'),
                    "href": doc.get('web_url'),
                    "body": doc.get('abstract') or doc.get('lead_paragraph') or "NYT Article"
                })
    except Exception as e:
        pass
    return results

def search_guardian(query, api_key, max_results=3):
    """
    Searches The Guardian Content API.
    """
    url = "https://content.guardianapis.com/search"
    params = {
        "q": query,
        "api-key": api_key,
        "show-fields": "headline,trailText,body",
        "page-size": max_results,
        "order-by": "relevance"
    }
    results = []
    try:
        response = requests.get(url, params=params, timeout=10)
        if response.status_code == 200:
            data = response.json()
            docs = data.get('response', {}).get('results', [])
            for doc in docs:
                fields = doc.get('fields', {})
                results.append({
                    "title": "[Guardian] " + fields.get('headline', doc.get('webTitle', 'No Title')),
                    "href": doc.get('webUrl'),
                    "body": fields.get('trailText') or "Guardian Article"
                })
    except Exception:
        pass
    return results

def search_web(query, category='general', language='de', max_results=5):
    """
    Searches DuckDuckGo and NYT (if available) with strategy based on category and language.
    """
    results = []
    region = 'de-de' if language == 'de' else 'wt-wt'
    
    keywords_map = {
        'medicine': { 'de': ' (Studie OR Medizin OR Faktencheck OR Gesundheit)', 'en': ' (Study OR Medicine OR Factcheck OR Health)' },
        'science': { 'de': ' (Wissenschaft OR Forschung OR Paper)', 'en': ' (Science OR Research OR Paper)' },
        'social': { 'de': ' (Fake OR Hoax OR Reddit OR Snopes)', 'en': ' (Fake OR Hoax OR Reddit OR Snopes)' },
        'tech': { 'de': ' (Technik OR Software OR Hardware OR Review)', 'en': ' (Tech OR Software OR Hardware OR Review)' },
        'finance': { 'de': ' (Finanzen OR Börse OR Wirtschaft)', 'en': ' (Finance OR Stock OR Economy)' }
    }

    # 1. DuckDuckGo (Primary Strategy)
    try:
        with DDGS() as ddgs:
            # STRATEGY 1: Pure News Search
            if category in ['news_politics', 'finance']:
                try:
                    q_news = query
                    news_gen = ddgs.news(q_news, region=region, max_results=max_results)
                    if news_gen:
                        for r in news_gen:
                            results.append({
                                "title": r.get('title'),
                                "href": r.get('url') or r.get('href'),
                                "body": r.get('body') or r.get('excerpt') or "News article"
                            })
                except Exception:
                    pass

            # STRATEGY 2: Text Search with Keywords
            # Always try to get at least 'max_results' total from DDG
            if len(results) < max_results:
                q_text = query
                if category in keywords_map:
                    cat_map = keywords_map[category]
                    suffix = cat_map.get(language, cat_map.get('de', ''))
                    q_text += suffix
                
                text_gen = ddgs.text(q_text, region=region, max_results=max_results)
                if text_gen:
                    for r in text_gen:
                        if not any(exist['href'] == r.get('href') for exist in results):
                            results.append({
                                "title": r.get('title'),
                                "href": r.get('href'),
                                "body": r.get('body')
                            })

    except Exception as e:
        # If DDG fails completely, we still rely on others
        pass

    # 2. International Sources (NYT / Guardian)
    # Append these AFTER the primary search results
    # SKIP if language is German (user feedback: they are irrelevant for local DE topics)
    if language != 'de':
        # Check for NYT Key
        nyt_key = os.getenv("NYT_API_KEY")
        if nyt_key:
            nyt_results = search_nyt(query, nyt_key)
            results.extend(nyt_results)

        # Check for Guardian Key
        guardian_key = os.getenv("GUARDIAN_API_KEY")
        if guardian_key:
            guardian_results = search_guardian(query, guardian_key)
            results.extend(guardian_results)
        
    return results[:10]

def get_nyt_feed(api_key, limit=20, offset=0):
    url = f"https://api.nytimes.com/svc/news/v3/content/all/all.json"
    params = { "api-key": api_key, "limit": limit, "offset": offset }
    results = []
    try:
        response = requests.get(url, params=params, timeout=10)
        if response.status_code == 200:
            data = response.json()
            docs = data.get('results', [])
            for doc in docs:
                results.append({
                    "title": doc.get('title', 'No Title'),
                    "href": doc.get('url'),
                    "body": doc.get('abstract', '') or "NYT Live Feed",
                    "published_date": doc.get('published_date', ''),
                    "byline": doc.get('byline', '')
                })
    except Exception as e:
        return [{"error": str(e)}]
    return results

def get_guardian_feed(api_key, limit=20, offset=0):
    page = (offset // limit) + 1
    url = "https://content.guardianapis.com/search"
    params = {
        "api-key": api_key,
        "page-size": limit,
        "page": page,
        "show-fields": "headline,trailText,body",
        "order-by": "newest"
    }
    results = []
    try:
        response = requests.get(url, params=params, timeout=10)
        if response.status_code == 200:
            data = response.json()
            docs = data.get('response', {}).get('results', [])
            for doc in docs:
                fields = doc.get('fields', {})
                results.append({
                    "title": "[Guardian] " + fields.get('headline', doc.get('webTitle', 'No Title')),
                    "href": doc.get('webUrl'),
                    "body": fields.get('trailText') or "Guardian Live",
                    "published_date": doc.get('webPublicationDate', ''), 
                    "byline": "The Guardian",
                    "source": "guardian"
                })
    except Exception as e:
        results.append({"error": str(e)})
    return results

def get_rss_feed(url, source_name, limit=20):
    """
    Parses a generic RSS feed.
    """
    import feedparser
    results = []
    try:
        feed = feedparser.parse(url)
        for entry in feed.entries[:limit]:
            results.append({
                "title": f"[{source_name}] " + entry.get('title', 'No Title'),
                "href": entry.get('link'),
                "body": entry.get('summary', '') or entry.get('description', '') or "RSS Entry",
                "published_date": entry.get('published', ''),
                "byline": source_name,
                "source": source_name.lower()
            })
    except Exception as e:
        results.append({"error": f"{source_name} Error: {str(e)}"})
    return results

def get_tagesschau_feed(limit=20):
    """
    Fetches news from Tagesschau API v2.
    """
    url = "https://www.tagesschau.de/api2/news/"
    results = []
    try:
        response = requests.get(url, timeout=10)
        if response.status_code == 200:
            data = response.json()
            news = data.get('news', [])
            for item in news[:limit]:
                results.append({
                    "title": "[Tagesschau] " + item.get('title', 'No Title'),
                    "href": item.get('shareURL') or item.get('detailsweb'),
                    "body": item.get('firstSentence', '') or "Tagesschau News",
                    "published_date": item.get('date', ''), 
                    "byline": "ARD-aktuell",
                    "source": "tagesschau"
                })
    except Exception as e:
        results.append({"error": "Tagesschau Error: " + str(e)})
    return results

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding='utf-8')

    if len(sys.argv) < 2:
        print(json.dumps({"error": "Usage: search_engine.py <query> [category] [language]"}))
        sys.exit(1)
    
    query = sys.argv[1]
    
    # 1. METADATA MODE
    # Supports separate args: python script.py METADATA_MODE "path/to/img"
    # Or single arg: python script.py "METADATA_MODE path/to/img"
    if query == "METADATA_MODE" and len(sys.argv) > 2:
        image_path = sys.argv[2]
        # Remove quotes if present
        image_path = image_path.strip('"')
        
        try:
            results = get_metadata(image_path)
            print(json.dumps(results, indent=2, ensure_ascii=False))
        except Exception as e:
             print(json.dumps({"error": str(e)}))
        sys.exit(0)

    if query.startswith("METADATA_MODE"):
        parts = query.split(maxsplit=1)
        if len(parts) < 2:
            print(json.dumps({"error": "No image path provided"}))
            sys.exit(1)
        image_path = parts[1].strip('"')
        try:
            results = get_metadata(image_path)
            print(json.dumps(results, indent=2, ensure_ascii=False))
        except Exception as e:
             print(json.dumps({"error": str(e)}))
        sys.exit(0)

    # 2. FEED MODE
    if query.startswith("FEED_MODE"):
        parts = query.split()
        source_filter = parts[1] if len(parts) > 1 else "all"
        offset = int(parts[2]) if len(parts) > 2 else 0

        all_results = []
        nyt_key = os.getenv("NYT_API_KEY")
        guardian_key = os.getenv("GUARDIAN_API_KEY")

        if (source_filter == "all" or source_filter == "nyt") and nyt_key:
            all_results.extend(get_nyt_feed(nyt_key, offset=offset))

        if (source_filter == "all" or source_filter == "guardian") and guardian_key:
             all_results.extend(get_guardian_feed(guardian_key, offset=offset))

        # German Sources (No API Key needed)
        if source_filter == "de_all" or source_filter == "tagesschau":
             all_results.extend(get_tagesschau_feed(limit=20))
        
        if source_filter == "de_all" or source_filter == "zeit":
             all_results.extend(get_rss_feed("https://newsfeed.zeit.de/index", "ZEIT ONLINE", limit=20))

        if source_filter == "de_all" or source_filter == "spiegel":
             all_results.extend(get_rss_feed("https://www.spiegel.de/schlagzeilen/tops/index.rss", "DER SPIEGEL", limit=20))
        
        try:
            all_results.sort(key=lambda x: x.get('published_date', ''), reverse=True)
        except: pass

        print(json.dumps(all_results, indent=2, ensure_ascii=False))
        sys.exit(0)

    # 3. LENS MODE (Legacy/Removed from UI but kept for engine support)
    if query.startswith("LENS_MODE"):
        # Legacy/Unused
        print(json.dumps([{"error": "LENS_MODE is deprecated"}]))
        sys.exit(0)

    # 4. DEFAULT SEARCH MODE
    category = sys.argv[2] if len(sys.argv) > 2 else 'general'
    language = sys.argv[3] if len(sys.argv) > 3 else 'de'
    
    data = search_web(query, category, language)
    print(json.dumps(data, indent=2, ensure_ascii=False))
