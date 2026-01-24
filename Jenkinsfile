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
                        sh 'find . -mindepth 1 -delete'
                        sh 'git config --global --add safe.directory "*"'
                        sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                        
                        // 1. Restore & Build
                        sh 'dotnet restore Pocketree.sln'
                        sh 'dotnet build Pocketree.sln -c Release'
                        
                        // 2. FIXED: Point to the CORRECT location shown in your logs (Source 3)
                        sh 'dotnet test src/Pocketree.Api.Tests/Pocketree.Api.Tests.csproj --no-build -c Release --logger "trx;LogFileName=TestResults.xml"'
                        
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
                        // 1. Install Git
                        sh 'apt-get update && apt-get install -y git'

                        sh 'find . -mindepth 1 -delete'
                        sh 'git config --global --add safe.directory "*"'
                        sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                        
                        // 2. FIXED: Go inside the folder where requirements.txt actually exists
                        dir('ml-service') {
                            sh 'pip install -r requirements.txt'
                            
                            // Smoke Test
                            sh 'python -c "import fastapi; print(\'FastAPI imported successfully\')"'
                        }
                    }
                }

                // STAGE 4: DATABASE CHECK
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
                    // Note: Ensure this path matches where your Dockerfile is. 
                    // Based on logs, it is likely in src/Pocketree.Api/Dockerfile
                    sh 'docker build -t pocketree-api -f src/Pocketree.Api/Dockerfile .'
                }
            }
        }
    }
}