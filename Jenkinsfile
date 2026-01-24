pipeline {
    agent none

    stages {
        stage('Parallel Build & Checks') {
            parallel {
                
                // --- 1. BACKEND (.NET) ---
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
                        
                        // 1. Restore & Build (Uses Pocketree.sln because you renamed the file)
                        sh 'dotnet restore Pocketree.sln'
                        sh 'dotnet build Pocketree.sln -c Release'
                        
                        // 2. Test (Uses ADProject path because the FOLDER is still named ADProject)
                        sh 'dotnet test tests/ADProject.Tests/ADProject.Tests.csproj --no-build -c Release --logger "trx;LogFileName=TestResults.xml"'
                        
                        archiveArtifacts artifacts: '**/TestResults.xml', allowEmptyArchive: true
                    }
                }

                // --- 2. ANDROID (Kotlin) ---
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

                // --- 3. ML SERVICE (Python) ---
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
                        sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                        
                        dir('ml-service') {
                            sh 'pip install -r requirements.txt'
                            sh 'python -c "import fastapi; print(\'FastAPI imported successfully\')"'
                        }
                    }
                }

                // --- 4. DATABASE CHECK ---
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
                    // FIX: Point to the ADProject folder for the Dockerfile
                    sh 'docker build -t pocketree-api -f src/ADProject.Api/Dockerfile .'
                }
            }
        }
    }
}