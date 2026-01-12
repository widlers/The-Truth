from duckduckgo_search import DDGS
import json

try:
    with DDGS() as ddgs:
        results = []
        print("Testing 'Merz besucht Indien' (news)...")
        news_gen = ddgs.news("Merz besucht Indien", region="de-de", max_results=5)
        if news_gen:
            for r in news_gen:
                results.append(r)
        
        print(f"News Results: {len(results)}")
        for r in results:
            print(f"- {r.get('title')}")

        print("\nTesting 'Merz besucht Indien' (text)...")
        text_results = []
        text_gen = ddgs.text("Merz besucht Indien", region="de-de", max_results=5)
        if text_gen:
            for r in text_gen:
                text_results.append(r)
        
        print(f"Text Results: {len(text_results)}")
        for r in text_results:
            print(f"- {r.get('title')}")

except Exception as e:
    print(f"Error: {e}")
