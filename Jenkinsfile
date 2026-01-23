pipeline {
    agent none

    stages {
        // --- PARALLEL BUILDS (ACTIVE) ---
        stage('Parallel Build & Checks') {
            parallel {
                // STAGE 1: BACKEND (.NET)
                stage('Backend Build & Security') {
                    agent { 
                        docker { 
                            image 'mcr.microsoft.com/dotnet/sdk:8.0' 
                        } 
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        // 1. Deep Clean & Clone
                        sh 'find . -mindepth 1 -delete'
                        sh 'git config --global --add safe.directory "*"'
                        sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                        
                        // 2. Build & Test
                        sh 'dotnet restore Pocketree.sln'
                        sh 'dotnet build Pocketree.sln -c Release'
                        sh 'dotnet test Pocketree.sln --no-build -c Release --logger "trx;LogFileName=TestResults.xml"'
                        
                        archiveArtifacts artifacts: '**/TestResults.xml', allowEmptyArchive: true
                    }
                }

                // STAGE 2: ANDROID (Kotlin)
                stage('Android Build & Lint') {
                    agent {
                        docker {
                            image 'mobiledevops/android-sdk-image:latest'
                            args '-u root'
                        }
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        sh 'find . -mindepth 1 -delete'
                        sh 'git config --global --add safe.directory "*"'
                        sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                        
                        dir('android-app/android-app') {
                            sh 'chmod +x gradlew'
                            sh './gradlew assembleDebug'
                            sh './gradlew lintDebug'
                        }
                        
                        archiveArtifacts artifacts: '**/*.apk', allowEmptyArchive: true
                        archiveArtifacts artifacts: '**/lint-results-debug.xml', allowEmptyArchive: true
                    }
                }

                // STAGE 3: ML SERVICE (Python 3.8)
                stage('ML Service Build & Test') {
                    agent { 
                        docker { 
                            image 'python:3.8-slim' 
                        } 
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        sh 'find . -mindepth 1 -delete'
                        sh 'git config --global --add safe.directory "*"'
                        sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                        
                        dir('ml-service') {
                            // 1. Install Dependencies
                            sh 'pip install -r requirements.txt'
                            
                            // 2. Smoke Test (Verify imports work)
                            sh 'python -c "import fastapi; print(\'FastAPI imported successfully\')"'
                            
                            // 3. Optional: Run full unit tests later
                            // sh 'python -m pytest'
                        }
                    }
                }

                // STAGE 4: DATABASE (MySQL)
                stage('Database Connectivity Check') {
                    agent { 
                        docker { 
                            image 'mysql:8.0' 
                            args '--network fixed-pocketree-network' 
                        } 
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        echo "Checking DB connectivity..."
                        // Uses the container name 'pocketree-db' to connect
                        sh 'mysql -h pocketree-db -u root -ppassword -e "SHOW DATABASES;"'
                    }
                }
            }
        }

        /* ================================================================
           DISABLED STAGES (Uncomment these when you are ready to deploy)
           ================================================================
        
        // --- SONARQUBE ANALYSIS ---
        stage('SonarQube Analysis') {
            agent { 
                docker { image 'mcr.microsoft.com/dotnet/sdk:8.0' } 
            }
            steps {
                // ... (Your SonarQube steps)
            }
        }

        // --- DOCKER BUILD & PUSH ---
        stage('Docker Build & Push') {
             // ... (Your Docker Push steps)
        }

        // --- DEPLOY TO AKS ---
        stage('Deploy to AKS') {
             // ... (Your Kubernetes steps)
        }
        */
    }
}