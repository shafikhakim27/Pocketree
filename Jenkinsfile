pipeline {
    agent none

    options {
        ansiColor('xterm') 
    }

    environment {
        // Defines the credentials ID for SonarQube steps to use
        SONAR_TOKEN = credentials('sonar-token') 
        // AZURE_CRED_ID = 'azure-sp-credentials'   
        // RAILWAY_HOOK = credentials('railway-webhook-url') 
    }

    stages {
        stage('Parallel Build & Checks') {
            parallel {
                
                // --- STAGE 1: BACKEND (.NET) ---
                stage('Backend Build & Test') {
                    agent { 
                        docker { image 'mcr.microsoft.com/dotnet/sdk:8.0' } 
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        sh 'find . -mindepth 1 -delete'
                        sh 'git clone -b develop https://github.com/shafikhakim27/Pocketree.git .'
                        
                        // 1. Install Java (REQUIRED for SonarScanner to run)
                        sh 'apt-get update && apt-get install -y openjdk-17-jre'
                        
                        // 2. Install SonarScanner Tool
                        sh 'dotnet tool install --global dotnet-sonarscanner'
                        
                        // 3. Run Analysis
                        withSonarQubeEnv('SonarQube-Server') {
                            sh '''
                            export PATH="$PATH:/root/.dotnet/tools"
                            
                            # Start SonarScanner
                            # /k = Key (Project Name), /d:sonar.token = Auth Token, /d:sonar.host.url = Server URL
                            dotnet sonarscanner begin /k:"pocketree-api" /d:sonar.token=$SONAR_TOKEN /d:sonar.host.url=$SONAR_HOST_URL /d:sonar.cs.vstest.reportsPaths=TestResults.xml
                            '''
                            
                            sh 'dotnet restore Pocketree.sln'
                            sh 'dotnet build Pocketree.sln -c Release'
                            
                            // Run Tests (Output to TestResults.xml)
                            sh 'dotnet test src/Pocketree.Api.Tests/Pocketree.Api.Tests.csproj --no-build -c Release --logger "trx;LogFileName=TestResults.xml"'
                            
                            sh '''
                            export PATH="$PATH:/root/.dotnet/tools"
                            
                            # Stop SonarScanner (Uploads data)
                            dotnet sonarscanner end /d:sonar.token=$SONAR_TOKEN
                            '''
                        }
                    }
                    post {
                        always {
                            junit '**/TestResults.xml'
                        }
                    }
                }

                // --- STAGE 2: ANDROID (Kotlin) ---
                stage('Android Build') {
                    agent {
                        docker {
                            image 'mobiledevops/android-sdk-image:latest'
                            args '-u root:root'
                        }
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        sh 'find . -mindepth 1 -delete'
                        sh 'git clone -b develop https://github.com/shafikhakim27/Pocketree.git .'
                        
                        dir('android-app/android-app') {
                            sh 'chmod +x gradlew'
                            sh './gradlew assembleDebug'
                            sh './gradlew lintDebug'
                        }
                        // Disabled: Signing (Uncomment when Keystore is uploaded)
                    }
                    post {
                        always {
                            archiveArtifacts artifacts: '**/*.apk', allowEmptyArchive: true
                            recordIssues(enabledForFailure: true, tool: androidLint(pattern: '**/lint-results.xml'))
                        }
                    }
                }

                // --- STAGE 3: ML SERVICE (Python) ---
                stage('ML Service Build') {
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
                        sh 'git clone -b develop https://github.com/shafikhakim27/Pocketree.git .'
                        
                        dir('ml-service') {
                            sh 'pip install -r requirements.txt'
                            sh 'python -c "import fastapi; print(\'FastAPI imported successfully\')"'
                        }
                    }
                }
            }
        }
    }
}