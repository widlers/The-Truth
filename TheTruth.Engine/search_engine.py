import sys
import json
from duckduckgo_search import DDGS

def search_web(query, category='general', language='de', max_results=5):
    """
    Searches DuckDuckGo with strategy based on category and language.
    """
    results = []
    # Map language 'en' -> 'wt-wt' (global/us) or 'en-us', 'de' -> 'de-de'
    region = 'de-de' if language == 'de' else 'wt-wt'
    
    # Keyword Injection map (Multilingual)
    keywords_map = {
        'medicine': {
            'de': ' (Studie OR Medizin OR Faktencheck OR Gesundheit)',
            'en': ' (Study OR Medicine OR Factcheck OR Health)'
        },
        'science': {
            'de': ' (Wissenschaft OR Forschung OR Paper)',
            'en': ' (Science OR Research OR Paper)'
        },
        'social': {
            'de': ' (Fake OR Hoax OR Reddit OR Snopes)',
            'en': ' (Fake OR Hoax OR Reddit OR Snopes)'
        },
        'tech': {
            'de': ' (Technik OR Software OR Hardware OR Review)',
            'en': ' (Tech OR Software OR Hardware OR Review)'
        },
        'finance': {
            'de': ' (Finanzen OR Börse OR Wirtschaft)',
            'en': ' (Finance OR Stock OR Economy)'
        }
    }

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
            if len(results) < 3:
                q_text = query
                # Add keywords if category matches
                if category in keywords_map:
                    # Get keywords for current language, fallback to de if missing
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

            results = results[:max_results]

    except Exception as e:
        return {"error": str(e)}
    
    return results

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding='utf-8')

    if len(sys.argv) < 2:
        print(json.dumps({"error": "Usage: search_engine.py <query> [category] [language]"}))
        sys.exit(1)
    
    query = sys.argv[1]
    category = sys.argv[2] if len(sys.argv) > 2 else 'general'
    language = sys.argv[3] if len(sys.argv) > 3 else 'de'
    
    data = search_web(query, category, language)
    print(json.dumps(data, indent=2, ensure_ascii=False))
