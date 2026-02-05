```mermaid
graph TD
    subgraph Mobile Pipelines
        direction LR
        subgraph "android-ci.yml (on push to main/develop)"
            A_CI_Push(Push to main/develop on android/**) --> A_CI_Build[build-android: Unit Test & Build APK]
        end
        subgraph "android-ui-test.yml (on push to main/develop)"
            A_UIT_Push(Push to main/develop on android/**) --> A_UIT_Test[ui-test: Run Maestro UI Tests]
        end
        subgraph "android-release.yml (on push to main)"
            A_Rel_Push(Push to main on android/**) --> A_Rel_Build[build-release-android: Build Release AAB]
        end
    end

    subgraph Web (API) Pipelines
        direction LR
        subgraph "api-ci.yml (on push to main/develop)"
            API_CI_Push(Push to main/develop on api/**) --> API_CI_Build[build: Snyk Scan & Build]
            API_CI_Build --> API_CI_Unit[unit-tests: Run Unit Tests]
            API_CI_Build --> API_CI_Integ[integration-tests: Run Integration Tests]
        end
        subgraph "api-cd.yml (on push to main)"
            API_CD_Push(Push to main on api/**) --> API_CD_Build[build-and-deploy: Build & Push Docker]
            API_CD_Build --> API_CD_Scan[scan: Trivy Scan]
            API_CD_Scan --> API_CD_Deploy[deploy: Deploy to Azure]
        end
    end

    subgraph ML Service Pipelines
        direction LR
        subgraph "ml-service-ci.yml (on push to main/develop)"
            ML_CI_Push(Push to main/develop on ml-service/**) --> ML_CI_Scan[security-scans: Snyk & Bandit]
        end
        subgraph "ml-service-cd.yml (on push to main)"
            ML_CD_Push(Push to main on ml-service/**) --> ML_CD_Build[build-and-deploy-ml: Build & Push Docker]
            ML_CD_Build --> ML_CD_Deploy[deploy: Deploy to Azure]
        end
    end
```

This Mermaid script visualizes your CI/CD pipelines, categorized by Mobile, Web (API), and ML. You can render this diagram in any Markdown editor or viewer that supports Mermaid, such as the online Mermaid Live Editor, or in your GitHub repositories.
