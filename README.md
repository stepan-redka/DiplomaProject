# RagSystem: Multi-Tenant RAG Research Platform

RagSystem is a robust Retrieval-Augmented Generation (RAG) platform developed using .NET 8, designed to facilitate research into document processing across varying data volumes and heterogeneous formats.

## Research Context
This system was developed as part of a Fourth-Year Diploma project titled: **"Research of approaches and capabilities of a RAG system for processing data of varying volume and types."**

### Core Research Features
- **Frictionless Onboarding Model:** Implementation of a "Value-first" access strategy allowing anonymous guest users to interact with the RAG pipeline before formal registration. This minimizes the initial interaction cost and facilitates immediate research engagement.
- **Guest Session Tracking:** Utilization of ephemeral session-based identifiers (`Guest-UUID`) to maintain stateful multi-tenant isolation for unauthenticated users without persistent database records.
- **Identity Upsell Framework:** Contextual UI gating for private features (e.g., document uploads, chat history), providing a seamless transition from guest exploration to authenticated research.
- **Asynchronous Background Ingestion:** Implementation of a high-throughput document processing pipeline using `System.Threading.Channels`. This decouples heavy parsing and indexing tasks from the web request cycle, ensuring high system responsiveness.
- **Batch Embedding Optimization:** Integration of batch-processing capabilities for high-dimensional vector generation, significantly reducing latency compared to sequential processing.
- **Scalable Ingestion Logic:** Support for large-scale document processing and manual text streams, enabling research into data volume impact on system performance.
- **Heterogeneous Data Extraction:** Specialized parsing pipeline for PDF, DOCX, Markdown, HTML, and Plain Text formats.
- **Dynamic Retrieval Tuning:** Real-time adjustment of the Top-K (Knowledge Depth) parameter to analyze the relationship between context density, response accuracy, and computational latency.

## Architecture and Methodology
The platform adheres to the principles of Clean Architecture to ensure strict separation of concerns and maintainability. This architectural choice supports the research by allowing isolated testing of retrieval and generation components.

### System Layering
- **Domain Layer:** Defines the core business entities (Document, ChatMessage, UserPreference) and the `IMultiTenant` interface which serves as the foundation for the data isolation strategy.
- **Application Layer:** Contains the service contracts and data transfer objects (DTOs). Orchestrates business logic without direct dependency on external frameworks.
- **Infrastructure Layer:** Provides concrete implementations for persistence (EF Core with PostgreSQL), vector operations (Qdrant), and AI orchestration (Microsoft.Extensions.AI).
- **Web Layer:** Implements the presentation logic using ASP.NET Core MVC and Razor Pages, providing a dashboard for research interaction.

### System Components Mapping
| Component | Implementation Detail | Research Purpose |
| :--- | :--- | :--- |
| **Orchestrator** | `RagService` | Coordination of retrieval and generation cycles. |
| **Vector Engine** | `QdrantVectorDatabase` | High-dimensional semantic search with multi-tenant filtering. |
| **Ingestion Worker** | `IngestionBackgroundService` | Non-blocking processing of heterogeneous data volumes. |
| **Analytics Engine** | `HealthService` & `RagService` stats | Quantitative monitoring of system performance. |
| **Export Engine** | `ExportService` (QuestPDF) | Formal reporting and session archival. |

## Technical Specification
- **Framework:** .NET 8
- **Vector Database:** Qdrant (Vector Similarity Search)
- **Relational Database:** PostgreSQL (Metadata and Chat History)
- **AI Integration:** Semantic Kernel and Microsoft.Extensions.AI
- **Front-end:** Tailwind CSS and Lucide icons for a modern, high-fidelity UI.

## Quick Start
To initialize the research environment and launch the application, utilize the provided orchestration tools:

### Using Makefile (Recommended)
```bash
# Start infrastructure (PostgreSQL, Qdrant)
make up

# Run the application
make run
```

### Using Shell Script
```bash
# Comprehensive build and launch
./start.sh
```

## Data Isolation and Security
Data security is managed through a multi-tier isolation strategy designed for multi-tenant research environments:
- **Logical Isolation:** Enforced at the data access layer via Entity Framework Core Global Query Filters.
- **Vector Isolation:** Search operations are restricted via Qdrant payload-based filtering tied to the User ID.
- **Metadata Interception:** Automated tagging of entities during the persistence lifecycle ensures data consistency across all research sessions.

## Performance and Scalability Optimization
The system has been optimized to handle varying data volumes and types through the following technical implementations:
- **Parallel Processing:** Utilization of `Parallel.ForEachAsync` for bulk operations in the vector database.
- **Batch Embedding:** Reduction of API overhead by grouping text chunks for embedding generation.
- **Memory Optimization:** Integration of `Microsoft.IO.RecyclableMemoryStream` to minimize memory fragmentation during large document parsing.
- **Payload Indexing:** Strategic indexing of user identifiers in the vector store to maintain low search latency across large datasets.

## Validation and Verification
The integrity of the research platform is verified through an automated testing suite:
- **Unit Testing:** Validation of chunking logic, parser routing, and DTO mapping.
- **Integration Testing:** Verification of end-to-end RAG flows and multi-tenant isolation correctness.
- **Background Task Validation:** Verification of asynchronous state transitions and error handling in the ingestion worker.

## Project Maturity and Final Results
The 14-day intensive development sprint concluded with a fully verified, production-ready RAG research platform. Key outcomes include:
- **Seamless Data Pipeline:** Successfully demonstrated end-to-end processing of diverse file formats with transparent source tracking.
- **Informed Retrieval:** Established a verifiable feedback loop for assessing response quality and effectiveness.
- **Enterprise Reporting:** Provided tools for high-fidelity export of research data for external analysis.
- **Robust Multi-Tenancy:** Confirmed strict data segregation across both relational and non-relational storage layers.

This platform serves as a foundational tool for advanced research into retrieval optimization and Large Language Model (LLM) performance in heterogeneous organizational contexts.
