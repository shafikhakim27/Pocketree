pipeline {
    agent none

    stages {
        stage('Parallel Build & Checks') {
            parallel {
                
                // STAGE 1: BACKEND (.NET)
                stage('Backend Build & Security') {
                    agent { 
                        docker { image 'mcr.microsoft.com/dotnet/sdk:8.0' } 
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        sh 'find . -mindepth 1 -delete'
                        sh 'git config --global --add safe.directory "*"'
                        
                        // FIX: Added '-b develop'
                        sh 'git clone -b develop https://github.com/shafikhakim27/Pocketree.git .'
                        
                        sh 'dotnet restore Pocketree.sln'
                        sh 'dotnet build Pocketree.sln -c Release'
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
                        
                        // FIX: Added '-b develop'
                        sh 'git clone -b develop https://github.com/shafikhakim27/Pocketree.git .'
                        
                        dir('android-app/android-app') {
                            sh 'chmod +x gradlew'
                            sh './gradlew assembleDebug'
                            sh './gradlew lintDebug'
                        }
                        
                        archiveArtifacts artifacts: '**/*.apk', allowEmptyArchive: true
                    }
                }

                // STAGE 3: ML SERVICE (Python 3.8)
                stage('ML Service Build & Test') {
                    agent { 
                        docker { 
                            image 'python:3.8-slim'
                            args '-u root:root'
                        } 
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        sh 'apt-get update && apt-get install -y git'
                        sh 'find . -mindepth 1 -delete'
                        sh 'git config --global --add safe.directory "*"'
                        
                        // FIX: Added '-b develop'
                        sh 'git clone -b develop https://github.com/shafikhakim27/Pocketree.git .'
                        
                        dir('ml-service') {
                            sh 'pip install -r requirements.txt'
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
                    sh 'docker build -t pocketree-api -f src/Pocketree.Api/Dockerfile .'
                }
            }
        }
    }
}