## File Upload System – Control Flow

```mermaid
sequenceDiagram
    participant User as User (Frontend - WPF)
    participant RedisQ as Redis (Queue)
    participant RedisPub as Redis (Pub/Sub)
    participant Worker as Worker Process
    participant API as API Server
    participant Storage as Storage (Local/Cloud)

    User->>RedisQ: Enqueue job (file metadata)
    User->>RedisPub: Subscribe to job status updates

    Worker->>RedisQ: Dequeue job
    Worker->>API: POST file upload
    API->>Storage: Save file
    Storage-->>API: Ack / Error
    API-->>Worker: Response (success/failure)

    alt Upload success
        Worker->>RedisPub: Publish "Completed"
        RedisPub-->>User: Notify "Completed"
    else Upload failure
        Worker->>RedisPub: Publish "Failed attempt"
        Worker->>RedisQ: Requeue job (retry, up to 5 times)
        RedisPub-->>User: Notify "Retry in progress"
    end

    alt Retry limit exceeded
        Worker->>RedisPub: Publish "Failed after 5 retries"
        RedisPub-->>User: Notify "Final failure"
    end

