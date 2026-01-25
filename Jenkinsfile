pipeline {
    agent none

    // 1. Colorize Output (Requires 'AnsiColor' plugin - Install this!)
    options {
        ansiColor('xterm') 
    }

    environment {
        // Placeholder credentials (uncomment and configure these later)
        // SONAR_TOKEN = credentials('sonar-token') 
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
                        
                        // --- DISABLED: SonarQube Analysis (Uncomment when Server is ready) ---
                        // sh 'apt-get update && apt-get install -y openjdk-17-jre'
                        // sh 'dotnet tool install --global dotnet-sonarscanner'
                        // withSonarQubeEnv('SonarQube-Server') {
                        //     sh '''
                        //     export PATH="$PATH:/root/.dotnet/tools"
                        //     dotnet sonarscanner begin /k:"pocketree-api" /d:sonar.login=$SONAR_TOKEN /d:sonar.host.url=$SONAR_HOST_URL
                        //     '''
                        
                        sh 'dotnet restore Pocketree.sln'
                        sh 'dotnet build Pocketree.sln -c Release'
                        
                        // Run Tests -> Output to TestResults.xml
                        sh 'dotnet test src/Pocketree.Api.Tests/Pocketree.Api.Tests.csproj --no-build -c Release --logger "trx;LogFileName=TestResults.xml"'
                        
                        //     sh 'export PATH="$PATH:/root/.dotnet/tools" && dotnet sonarscanner end /d:sonar.login=$SONAR_TOKEN'
                        // }
                    }
                    post {
                        always {
                            // This works immediately!
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
                            sh './gradlew assembleDebug' // Build Debug version (No signing needed)
                            sh './gradlew lintDebug'
                        }

                        // --- DISABLED: Android Signing (Uncomment when Keystore is uploaded) ---
                        // signAndroidApks(
                        //    keyStoreId: 'android-keystore-creds',
                        //    keyAlias: 'pocketree-key',
                        //    apksToSign: '**/*-release-unsigned.apk',
                        //    archiveSignedApks: true,
                        //    archiveUnsignedApks: false
                        // )
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
        
        // --- STAGE 4: DEPLOYMENT (DISABLED UNTIL CLOUD SETUP) ---
        /*
        stage('Deploy to Cloud') {
            parallel {
                stage('Deploy to Azure') {
                    agent any
                    steps {
                        azureWebAppPublish(
                            azureCredentialsId: env.AZURE_CRED_ID,
                            resourceGroup: 'pocketree-rg',
                            appName: 'pocketree-api',
                            dockerImageName: 'shafikhakim/pocketree-api',
                            dockerImageTag: 'latest'
                        )
                    }
                }
                
                stage('Deploy to Railway') {
                    agent any
                    steps {
                        httpRequest(
                            url: env.RAILWAY_HOOK,
                            httpMode: 'POST',
                            validResponseCodes: '200'
                        )
                    }
                }
            }
        }
        */
    }
}