# FileUplaoder
Native windows based File Uploader which will upload the selected files to a cloud storage. 

04/09:
    1. Added SDKs for
        a. Client
        b. API Server
        c. Backend Worker Service

    2. Created git repo in Github and added a pipeline to build and test the changes on main branch.

    3. Tested the components individually. Redis integration pending to test the whole flow.
    PENDING:
        add redis container
        add scripts to deploy the appln
        add unit tests
        refactor everything into LLD patterns
        create HLD diagram
            Note HLD improvements for future
        create LLD diagram
            Note LLD improvements for future
        think of ways to enhance for future
        create PPT
        study about AI workflows
        DSA!!!

k2s image build Dockerfile --image-name fileuploader --image-tag latest --windows


Deploy redis
    kubectl apply -k D:\Sailesh\bin\x64\debug\Redis
    kubectl port-forward svc/redis 6379:6379 -n file-uploader
    

k2s image build Dockerfile --image-name fileuploader --image-tag new0409.3 --windows