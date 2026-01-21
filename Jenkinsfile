pipeline {
    agent none

    stages {
        // --- PARALLEL BUILDS ---
        stage('Parallel Build & Checks') {
            parallel {
                stage('Backend Build & Security') {
                    agent { 
                        docker { 
                            image 'mcr.microsoft.com/dotnet/sdk:8.0' 
                        } 
                    }
                    steps {
                        checkout scm  // <--- NEW: Clones code into this isolated workspace
                        sh 'dotnet restore Pocketree.sln'
                        sh 'dotnet build Pocketree.sln -c Release'
                        sh 'dotnet test Pocketree.sln --no-build --logger "trx;LogFileName=TestResults.xml"'
                    }
                }

                stage('Android Build & Lint') {
                    agent {
                        docker {
                            image 'mobiledevops/android-sdk-image:latest'
                            args '-u root'
                        }
                    }
                    steps {
                        checkout scm  // <--- NEW: Fixes "fatal: not in a git directory"
                        
                        dir('android-app/android-app') {
                            sh 'chmod +x gradlew'
                            sh './gradlew testDebugUnitTest'
                            sh './gradlew lintDebug'
                            sh './gradlew assembleDebug'
                        }
                    }
                }

                stage('Database Connectivity Check') {
                    agent { 
                        docker { 
                            image 'mysql:8.0' 
                            // NEW: Joins the specific network so it can find "pocketree-db"
                            args '--network pocketree_pocketree-net' 
                        } 
                    }
                    steps {
                        echo "Checking DB connectivity..."
                        [cite_start]// Host 'pocketree-db' is defined in your docker-compose.yml [cite: 21]
                        sh 'mysql -h pocketree-db -u root -ppassword -e "SHOW DATABASES;"'
                    }
                }
            }
        }

        // --- SONARQUBE ANALYSIS ---
        stage('SonarQube Analysis') {
            agent { 
                docker { 
                    image 'mcr.microsoft.com/dotnet/sdk:8.0' 
                    // Optional: Add network here if SonarQube is also running in Docker
                } 
            }
            steps {
                checkout scm // <--- NEW: Ensure code exists for analysis
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
            steps {
                checkout scm // <--- NEW: Need code to build the image
                script {
                    // Note: Ensure your Docker Registry credentials are set in Jenkins
                    def appImage = docker.build("pocketree-api:${env.BUILD_ID}", "src/Pocketree.Api")
                    // Uncomment below when ready to push
                    // docker.withRegistry("https://<ACR_NAME>.azurecr.io", "acr-credentials") {
                    //    appImage.push()
                    // }
                }
            }
        }

        // --- DEPLOY TO AKS ---
        stage('Deploy to AKS') {
            agent { docker { image 'bitnami/kubectl:latest' } }
            steps {
                checkout scm // <--- NEW: Need k8s/deployment.yaml
                withCredentials([azureServicePrincipal('azure-sp')]) {
                    // Update variables <RG_NAME> etc. before running
                    sh '''
                        # az login --service-principal -u $APP_ID -p $APP_SECRET --tenant $TENANT_ID
                        # az aks get-credentials --resource-group <RG_NAME> --name <AKS_NAME>
                        # kubectl apply -f k8s/deployment.yaml
                        echo "Deployment placeholder"
                    '''
                }
            }
        }
    }

    post {
        always {
            mstest testResultsFile: '**/TestResults.xml', keepLongStdio: true
            archiveArtifacts artifacts: '**/*.apk', allowEmptyArchive: true
            archiveArtifacts artifacts: '**/lint-results-debug.xml', allowEmptyArchive: true
        }
    }
}