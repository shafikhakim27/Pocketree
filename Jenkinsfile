pipeline {
    agent none

    stages {
        // --- PARALLEL BUILDS ---
        stage('Parallel Build & Checks') {
            parallel {
                stage('Backend Build & Security') {
                    agent { docker { image 'mcr.microsoft.com/dotnet/sdk:8.0' } }
                    steps {
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
                        dir('android-app/android-app') {
                            sh 'chmod +x gradlew'
                            sh './gradlew testDebugUnitTest'
                            sh './gradlew lintDebug'
                            sh './gradlew assembleDebug'
                        }
                    }
                }

                stage('Database Check') {
                    agent { docker { image 'mysql:8.0' } }
                    steps {
                        sleep 10
                        sh 'mysql -h pocketree-db -u root -ppassword < init.sql'
                    }
                }
            }
        }

        // --- SONARQUBE ANALYSIS ---
        stage('SonarQube Analysis') {
            agent { docker { image 'mcr.microsoft.com/dotnet/sdk:8.0' } }
            steps {
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
            agent { label 'docker' }   // run on Jenkins node with Docker installed
            steps {
                script {
                    def appImage = docker.build("pocketree-api:${env.BUILD_ID}", "src/Pocketree.Api")
                    docker.withRegistry("https://<ACR_NAME>.azurecr.io", "acr-credentials") {
                        appImage.push()
                    }
                }
            }
        }

        // --- DEPLOY TO AKS ---
        stage('Deploy to AKS') {
            agent { docker { image 'bitnami/kubectl:latest' } }
            steps {
                withCredentials([azureServicePrincipal('azure-sp')]) {
                    sh '''
                        az login --service-principal -u $APP_ID -p $APP_SECRET --tenant $TENANT_ID
                        az aks get-credentials --resource-group <RG_NAME> --name <AKS_NAME>
                        kubectl apply -f k8s/deployment.yaml
                    '''
                }
            }
        }
    }

    post {
        always {
            // Archive test results and artifacts
            mstest testResultsFile: '**/TestResults.xml', keepLongStdio: true
            archiveArtifacts artifacts: '**/*.apk', allowEmptyArchive: true
            archiveArtifacts artifacts: '**/lint-results-debug.xml', allowEmptyArchive: true
        }
    }
}