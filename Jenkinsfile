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
                        // Check if your test folder is named Pocketree.Tests or ADProject.Tests
                        sh 'dotnet test tests/Pocketree.Tests/Pocketree.Tests.csproj --no-build --verbosity normal'
                        
                        archiveArtifacts artifacts: '**/TestResults.xml', allowEmptyArchive: true
                    }
                }

                // STAGE 2: ANDROID (Kotlin)
                stage('Android Build & Lint') {
                    agent {
                        docker {
                            image 'mobiledevops/android-sdk-image:latest'
                            args '-u root:root'
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
                            // Run as root to install git
                            args '-u root:root'
                        } 
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        // 1. FIX: Install Git first!
                        sh 'apt-get update && apt-get install -y git'

                        sh 'find . -mindepth 1 -delete'
                        sh 'git config --global --add safe.directory "*"'
                        sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                        
                        dir('ml-service') {
                            // 2. Install Dependencies
                            sh 'pip install -r requirements.txt'
                            
                            // 3. Smoke Test
                            sh 'python -c "import fastapi; print(\'FastAPI imported successfully\')"'
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
                        // FIX: Changed password to 'rootpassword'
                        sh 'mysql -h pocketree-db -u root -prootpassword -e "SHOW DATABASES;"'
                    }
                }
            }
        }
        
        // --- DOCKER BUILD (Sequential) ---
        stage('Docker Image Build') {
            agent any
            steps {
                script {
                    sh 'docker build -t pocketree-api -f src/Pocketree.Api/Dockerfile .'
                }
            }
        }
    }
}