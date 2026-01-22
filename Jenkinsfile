pipeline {
    agent none

    stages {
        // --- PARALLEL BUILDS (ACTIVE) ---
        stage('Parallel Build & Checks') {
            parallel {
                // STAGE 1: BACKEND
                stage('Backend Build & Security') {
                    agent { 
                        docker { 
                            image 'mcr.microsoft.com/dotnet/sdk:8.0' 
                        } 
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        // 1. Deep Clean (Removes hidden files preventing clone)
                        sh 'find . -mindepth 1 -delete'
                        
                        // 2. Git Config (Trusts current directory)
                        sh 'git config --global --add safe.directory "*"'
                        
                        // 3. Manual Clone
                        sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                        
                        sh 'dotnet restore Pocketree.sln'
                        sh 'dotnet build Pocketree.sln -c Release'
                        
                        // 4. Test (Release mode to match build)
                        sh 'dotnet test Pocketree.sln --no-build -c Release --logger "trx;LogFileName=TestResults.xml"'
                        
                        archiveArtifacts artifacts: '**/TestResults.xml', allowEmptyArchive: true
                    }
                }

                // STAGE 2: ANDROID
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

                // STAGE 3: DATABASE
                stage('Database Connectivity Check') {
                    agent { 
                        docker { 
                            image 'mysql:8.0' 
                            args '--network pocketree_pocketree-net' 
                        } 
                    }
                    options { skipDefaultCheckout() }
                    
                    steps {
                        echo "Checking DB connectivity..."
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
                docker { 
                    image 'mcr.microsoft.com/dotnet/sdk:8.0' 
                } 
            }
            options { skipDefaultCheckout() }
            steps {
                sh 'find . -mindepth 1 -delete'
                sh 'git config --global --add safe.directory "*"'
                sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                
                withSonarQubeEnv('SonarQubeServer') {
                    sh '''
                        dotnet sonarscanner begin /k:"Pocketree" /d:sonar.login=$SONARQUBE_TOKEN
                        dotnet build Pocketree.sln
                        dotnet sonarscanner end /d:sonar.login=$SONARQUBE_TOKEN
                    '''
                }
            }
        }

        // --- DOCKER BUILD & PUSH ---
        stage('Docker Build & Push') {
            agent { label 'docker' }   
            options { skipDefaultCheckout() }
            steps {
                sh 'find . -mindepth 1 -delete'
                sh 'git config --global --add safe.directory "*"'
                sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                
                script {
                    def appImage = docker.build("pocketree-api:${env.BUILD_ID}", "src/Pocketree.Api")
                    // docker.withRegistry("https://<ACR_NAME>.azurecr.io", "acr-credentials") {
                    //    appImage.push()
                    // }
                }
            }
        }

        // --- DEPLOY TO AKS ---
        stage('Deploy to AKS') {
            agent { docker { image 'bitnami/kubectl:latest' } }
            options { skipDefaultCheckout() }
            steps {
                sh 'find . -mindepth 1 -delete'
                sh 'git config --global --add safe.directory "*"'
                sh 'git clone https://github.com/shafikhakim27/Pocketree.git .'
                
                withCredentials([azureServicePrincipal('azure-sp')]) {
                    sh 'echo "Simulating deployment to AKS..."'
                }
            }
        }
        */
    }
}