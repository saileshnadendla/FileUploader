# FileUplaoder
Native windows based File Uploader which will upload the selected files to a cloud storage. 

## Functional Requirements
1. File Upload - Client can upload 1 or more files to a REST / Cloud endpoint.
2. Pause & Resume - Uploads to be resumed upon network restore.
3. Progress Tracking - Track the progress of an upload
4. Download - Users can download the files they have successfully uploaded
5. Persistence - Upload Progress / state of a user is persisted when user restarts the client.

## Non-Functional Requirements
1. Reliability - No file is lost during network interruptions
2. Scalability - Should support multiple files and large file sizes
3. Extensibility - Support cloud storage
4. Performance - Efficiently handle large file uploads
5. Security - Files must be transferred securely
6. Usability - UI should give a clear indication to the user

## Assumptions
1. Client runs on windows desktop - Developed using WPF
2. File Size limited to small / medium ( < 1GB)
3. Storage target is local folder (not cloud, though design allows extension to cloud storage)
4. Prototype works well for single client
5. Files are retried a finite number of times before sending a failure signal
6. Job IDs generated using GUIDs - Collision probability is near zero.
7. Redis availability assumed - if redis is down, client fallbacks to local queue until redis connection is restored

## Tech Stack used
1. Client - WPF
2. Worker Service - .NET Background service
3. API Server - .NET Core Web API
4. Messaging & Coordination - Redis
5. Storage - Local File Storage
6. NUnit for unit testing

## Steps to Run the Application
0. Ensure Kubernetes is supported
1. clone the repository to a local folder (e.g., D:/Workspaces)
2. Compile FileUploader.sln (./FileUploader/FileUploader.sln)
3. To Start the application - Run the powershell script ../bin/x64/Debug/Deploy/Scripts/start.ps1
4. To Stop the application - Run the powershell script ../bin/x64/Debug/Deploy/Scripts/stop.ps1
