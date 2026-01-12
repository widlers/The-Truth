# The-Truth: Verification Engine 🕵️‍♂️✅

**The-Truth** is an open-source, privacy-first desktop application that verifies claims by cross-referencing them with real-time internet search results and analyzing them using local AI (Ollama).

![German Interface](screenshot_de.png)
*German Interface (Dark Mode)*

![English Interface](screenshot_us.png)
*English Interface with US Search Region*

## 🚀 Features

*   **Live Verification**: Enters a claim -> Searches the Web -> Analyzes with AI.
*   **Visual Forensics (NEW)**: Media Check Tab allows analyzing images for fake content (Deepfakes, Photoshop) using BakLLaVA. Includes **Metadata Analysis** (EXIF, C2PA, AI Traces) and **Reverse Search** (Google Lens integration).
*   **Privacy Focused**: Uses local AI (**Ollama**) and anonymous search (**DuckDuckGo**). No data leaves your machine for analysis.
*   **Smart Search**: Specialized modes for *News*, *Medicine*, *Finance*, *Tech*, and *Social Media*.
*   **News Feed**: Live news ticker. Automatically switches to German sources (**Tagesschau**, **Zeit**, **Spiegel**) when language is set to DE 🇩🇪.
*   **Multilingual**: Full support for **English** 🇺🇸 and **German** 🇩🇪 (UI, Search Results, and AI Analysis).
*   **Context Aware**: Understands political context (e.g., "Trump" = "US President").

## 🛠️ Technology Stack

*   **Frontend**: C# / Avalonia UI (.NET 9)
*   **Backend Search**: Python (`duckduckgo_search`, `c2pa-python`, `Pillow`)
*   **Intelligence**: Local LLM via [Ollama](https://ollama.com/) (Llama3, Mistral, BakLLaVA, etc.)

## 📦 Prerequisites

1.  **Ollama**: Installed and running (`ollama serve`).
    *   Pull a text model: `ollama pull llama3` (or mistral).
    *   Pull a vision model: `ollama pull bakllava` (for Media Check).
2.  **.NET 9 SDK**: Required to build/run the UI.
3.  **Python 3.x**: Required for the search engine.

## 📥 Installation

1.  **Clone the repository**:
    ```bash
    git clone https://github.com/Start-Process/The-Truth.git
    cd The-Truth
    ```

2.  **Install Python Dependencies**:
    ```bash
    pip install -r TheTruth.Engine/requirements.txt
    ```

3.  **Build and Run**:
    ```bash
    cd TheTruth.UI
    dotnet run
    ```

## 🎮 Usage

1.  **Start the App**.
2.  **Select Language** (top right): 🇩🇪 or 🇺🇸.
3.  **Use Tabs**:
    *   **VERIFY**: Enter a text claim (e.g., "Earth is flat") -> Click VERIFY.
    *   **NEWS FEED**: View live news (Tagesschau/Zeit in DE, NYT/Guardian in EN).
    *   **MEDIA CHECK**: Drag & Drop an image to analyze it for AI manipulation or fake context. Use "Check Metadata" for technical details.
4.  **Drag & Drop**: You can drag images from the Media Check preview directly to your browser (e.g. into Google Lens).

## 🤝 Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## 📄 License

MIT License. See [LICENSE](LICENSE) for details.
