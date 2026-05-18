# RagSystem - Enterprise-Grade Local Multi-Tenant RAG Platform

**Executive Overview**
 Retrieval-Augmented Generation (RAG) platform engineered in **.NET 8**. It features autonomous machine learning intent routing, high-dimensional semantic caching, and multi-format ingestion pipelines. The system is designed for complete operational autonomy, executing all LLM and embedding operations within a containerized local infrastructure via **Ollama** and **Semantic Kernel**, ensuring strict data privacy and zero external API dependencies.

---

## Core Architectural Pillars

The solution adheres to a strict 4-layer Clean Architecture, ensuring clear separation between business logic and technical execution.

### Domain Layer (Diploma.Domain)
Contains pure enterprise entities and core business rules. It enforces strict multi-tenant isolation through the **IMultiTenant** interface and global query filters.
- **Entities:** ChatSession, ChatMessage, Document, DocumentChunk.

### Application Layer (Diploma.Application)
Defines domain-grouped use-case abstractions, completely decoupled from infrastructure implementations.
- **Structure:** AI/, Chat/, Documents/, Analytics/, Identity/, Storage/.
- **Contracts:** Enforces the Interface Segregation Principle across all research and retrieval operations.

### Infrastructure Layer (Diploma.Infrastructure)
The technical execution engine managing concrete boundaries with containerized services.
- **Orchestration:** Integrated with **Qdrant** (Vector DB), **PostgreSQL** (Metadata/Identity), and **Ollama** (Inference).
- **Inference:** Executes local LLMs (Llama 3.1, Qwen 2.5, Phi 3.5) and embedding models (nomic-embed-text).

---

## Advanced Ingestion & Parsing Pipeline

The system employs a non-blocking, asynchronous ingestion lifecycle managed via the **IngestionBackgroundService**.

- **Thread-Safe Orchestration:** Utilizes **IngestionChannel** and **RecyclableMemoryStream** for efficient, zero-allocation memory management during high-volume document processing.
- **Polymorphic Parsing Engine:** Supports an extensible range of formats through specialized **IDocumentParser** implementations:
  - **Scientific/Academic:** PdfDocumentParser, LatexDocumentParser.
  - **Technical/Structured:** CodeDocumentParser, MarkdownDocumentParser, ExcelDocumentParser, CsvDocumentParser.
- **Vectorization:** Chunks are vectorized and serialized to Qdrant with user-specific payload tags to ensure logical data isolation.

---

## 3-Tier Execution & Intent Routing Engine

RagSystem utilizes a deterministic routing engine to classify incoming queries, preventing redundant pipeline execution and optimizing GPU/CPU throughput.

```text
[Incoming Query]
       |
       ├─► (Level 0: Manual Override) ────────► Returns RESEARCH (Force RAG)
       |
       ├─► (Level 1: Zero-Document Fallback) ──► Returns GENERAL (Skip RAG)
       |
       └─► (Level 2: Autonomous ML Inference) ─► [ML.NET Classifier]
                                                       │
                                               Mapped to QueryIntent Enum
```

**Implementation Detail:**
The **IntentResolver** uses a local **ML.NET Logistic Regression** model trained on the **CLINC150** dataset. It distinguishes between "General Talk" and "Deep Research" (RESEARCH), ensuring that expensive vector searches are only performed when semantically necessary.

---

## Performance Defense: Semantic Vector Caching

To achieve sub-second response times in local LLM environments, RagSystem implements a **Cache-Aside** semantic caching layer.

- **Mechanism:** Incoming queries are vectorized and compared against a dedicated **cached_queries** collection in Qdrant.
- **Interception:** A high-dimensional cosine similarity lookup is performed. If a match is identified with a threshold **Similarity >= 0.95**, the system bypasses the entire RAG/LLM pipeline.
- **Impact:** Reduces response latency from several seconds to **single-digit milliseconds (~5ms)**, significantly extending hardware lifespan and improving user experience.

---

## Analytical & Evaluation Framework

- **Deterministic Metrics:** The **EvaluationService** converts raw vector distance values into human-readable 0-100% metrics, providing a clear "Trust Score" for retrieved context.
- **Resource Tracking:** The **TokenizerService** provides real-time tracking of token consumption and context window utilization, ensuring stable execution within the limits of local models.

---

## Solution Repository Layout

```text
RagSystem/
├── Diploma.Application/           # Domain-grouped interfaces & DTOs
├── Diploma.Domain/                # Pure enterprise entities & enums
├── Diploma.Infrastructure/        # Technical execution & service implementations
│   ├── ML/                        # Intent model training logic
│   ├── Persistence/               # SQL and Vector DB contexts
│   └── Services/                  # Tiered folders (AI, Chat, Documents, etc.)
├── Diploma.Web/                   # Core Web Node (MVC + Identity)
└── Diploma.Tests/                 # Enterprise Verification Suite (XUnit/Moq)
```

---

## Deployment & Execution Quickstart

The entire platform is designed for **Container-Native** deployment using Docker Compose V2.

### Prerequisites
- **Docker & Docker Compose (V2)**
- **NVIDIA Container Toolkit** (Optional for GPU acceleration)
- **.NET 8 SDK** (For local development/tests)

### Step-by-Step Orchestration
1. **Full System Start**:
   Executes a clean reset, rebuilds containers, and initializes the AI model suite:
   ```bash
   make start
   ```
2. **AI Platform Provisioning**:
   The `ollama-setup` container automatically pulls the required model stack:
   - **LLMs:** `llama3.1`, `qwen2.5:7b`, `phi3.5`.
   - **Embeddings:** `nomic-embed-text`.
3. **Model Training**:
   On the first run, the system automatically trains the intent classifier (`intent_model.zip`) using the localized dataset.
4. **Verification**:
   ```bash
   dotnet test
   ```
   All **23 enterprise-level tests** (Intent Routing, Multi-tenancy, Semantic Cache) are verified green.
