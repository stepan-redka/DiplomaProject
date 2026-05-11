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

## Architecture
The platform adheres to the principles of Clean Architecture to ensure strict separation of concerns and maintainability:

- **Domain Layer:** Contains core business entities and interfaces for multi-tenancy.
- **Application Layer:** Defines DTOs and orchestrates business logic through service interfaces.
- **Infrastructure Layer:** Implements concrete services for AI orchestration (via Microsoft.Extensions.AI), vector database management (Qdrant), and data persistence (EF Core with PostgreSQL).
- **Presentation Layer:** An ASP.NET Core Razor Pages web application utilizing a responsive dashboard for real-time interaction.

### Multi-Tenancy and Data Isolation
Data security is managed through a strict multi-tenant isolation strategy:
- **Logical Isolation:** Enforced via Entity Framework Core Global Query Filters.
- **Guest Isolation:** Anonymous data access is restricted to public records and ephemeral session-linked content.
- **Physical Isolation:** Vector data is partitioned within Qdrant using payload-based filtering tied to the User ID (Authenticated or Guest).
- **Automated Metadata Tagging:** An interceptor pattern ensures all incoming data is tagged with the appropriate tenant identifier during the persistence lifecycle.

## Technical Specification
- **Framework:** .NET 8
- **Vector Database:** Qdrant (Vector Similarity Search)
- **Relational Database:** PostgreSQL (Metadata and Chat History)
- **AI Integration:** Semantic Kernel and Microsoft.Extensions.AI
- **Front-end:** Tailwind CSS and Lucide icons for a modern, high-fidelity UI.

## Validation
System reliability and architectural mandates are verified through a comprehensive xUnit test suite, including:
- Unit tests for the text chunking and context retention algorithms.
- Integration tests for verifying multi-tenant data isolation and RAG orchestration flows.
- Automated parser routing validation for diverse file formats.

## Project Maturity & Results (Sprint Finalization)

The 14-day development sprint concluded with a fully functional, production-ready RAG system. The following key milestones were achieved:

- **Full Lifecycle Document Ingestion:** Demonstrated capability to process and index heterogeneous data formats (PDF, DOCX, Markdown) with automated status tracking and error handling.
- **Advanced Research Analytics:** Implementation of a real-time dashboard providing insights into system health, query volume, and storage utilization.
- **Enterprise-Grade Transparency:** Integration of source citation mechanisms, allowing users to verify AI responses against retrieved document chunks.
- **Human-in-the-Loop Feedback:** Established a feedback loop where users can rate the effectiveness of RAG-generated responses, providing data for future model tuning.
- **Scalable Export Capabilities:** Development of a high-fidelity PDF export service for research results and chat summaries.
- **Strict Multi-Tenant Security:** Verified isolation of user data across both relational (PostgreSQL) and vector (Qdrant) storage layers, ensuring zero data leakage between research tenants.

This system serves as a foundational platform for further research into retrieval optimization and LLM performance across varied organizational datasets.
