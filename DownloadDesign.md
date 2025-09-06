## File Upload System – Control Flow

```mermaid
sequenceDiagram
    participant User as User (Frontend - WPF)
    participant API as API Server
    participant Storage as Storage (Local/Cloud)

    User->>API: Click "Download" → GET file (JobId)
    API->>Storage: Fetch file (by JobId)
    Storage-->>API: Return file
    API-->>User: Return file (download dialog/save to disk)