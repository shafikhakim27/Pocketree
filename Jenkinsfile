pipeline {
    agent none

    options {
        ansiColor('xterm')
        timestamps()
        timeout(time: 1, unit: 'HOURS')
    }

    environment {
        // Build configurations
        DOTNET_SDK = '9.0'
        REGISTRY = 'docker.io'
        IMAGE_NAME = 'pocketree-api'
        IMAGE_TAG = "${BUILD_NUMBER}"
        
        // SonarQube (optional)
        SONAR_TOKEN = credentials('sonar-token')
        SONAR_HOST_URL = 'http://sonarqube:9000'
    }

    stages {
        stage('Setup') {
            agent { label 'master' }
            steps {
                script {
                    echo "Setting up build environment..."
                    sh '''
                    # Create Docker network if not exists
                    docker network create pocketree-network || true
                    '''
                }
            }
        }

        stage('Parallel Build & Checks') {
            parallel {
                
                // BACKEND (.NET 9.0)
                stage('Backend Build & Test') {
                    agent {
                        docker {
                            image "mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK}"
                            args '--network pocketree-network -u 0:0 --rm'
                            reuseNode true
                        }
                    }
                    steps {
                        checkout scm
                        
                        script {
                            sh '''
                            # Restore
                            dotnet restore Pocketree.sln
                            
                            # Build Release
                            dotnet build Pocketree.sln -c Release --no-restore
                            
                            # Run Tests (if tests exist)
                            if [ -f "src/Pocketree.Api.Tests/Pocketree.Api.Tests.csproj" ]; then
                                dotnet test src/Pocketree.Api.Tests/Pocketree.Api.Tests.csproj \
                                    -c Release \
                                    --no-build \
                                    --logger "trx;LogFileName=TestResults.xml" \
                                    --verbosity normal || true
                            fi
                            '''
                        }
                    }
                    post {
                        always {
                            junit '**/TestResults.xml' || true
                            archiveArtifacts artifacts: '**/bin/Release/**/*.dll', allowEmptyArchive: true
                        }
                    }
                }

                // ANDROID BUILD
                stage('Android Build') {
                    agent {
                        docker {
                            image 'mobiledevops/android-sdk-image:latest'
                            args '--network pocketree-network -u root:root --rm'
                            reuseNode true
                        }
                    }
                    steps {
                        checkout scm
                        
                        dir('android-app/android-app') {
                            sh '''
                            chmod +x gradlew
                            ./gradlew clean assembleDebug lintDebug
                            '''
                        }
                    }
                    post {
                        always {
                            archiveArtifacts artifacts: '**/outputs/apk/**/*.apk', allowEmptyArchive: true
                            recordIssues enabledForFailure: false, tool: androidLintParser(pattern: '**/lint-results.xml') || true
                        }
                    }
                }

                // ML SERVICE (Python)
                stage('ML Service Build') {
                    agent {
                        docker {
                            image 'python:3.8-slim'
                            args '--network pocketree-network -u 0:0 --rm'
                            reuseNode true
                        }
                    }
                    steps {
                        checkout scm
                        
                        dir('ml-service') {
                            sh '''
                            pip install --upgrade pip
                            pip install -r requirements.txt
                            
                            # Validate imports
                            python -c "import fastapi; print('FastAPI OK')"
                            python -c "import uvicorn; print('Uvicorn OK')"
                            '''
                        }
                    }
                }
            }
        }

        stage('Build Docker Images') {
            agent { label 'master' }
            steps {
                checkout scm
                
                script {
                    sh '''
                    # Backend
                    docker build \
                        -f src/Pocketree.Api/Dockerfile \
                        -t ${IMAGE_NAME}:${IMAGE_TAG} \
                        -t ${IMAGE_NAME}:latest \
                        .
                    
                    # ML Service
                    docker build \
                        -f ml-service/Dockerfile \
                        -t pocketree-ml:${IMAGE_TAG} \
                        -t pocketree-ml:latest \
                        ml-service/
                    '''
                }
            }
        }

        stage('Integration Tests') {
            agent { label 'master' }
            steps {
                script {
                    sh '''
                    # Start services
                    docker-compose -f docker-compose.yml up -d
                    
                    # Wait for services
                    sleep 15
                    
                    # Test backend health
                    curl -f http://localhost:5042/ || exit 1
                    
                    # Cleanup
                    docker-compose -f docker-compose.yml down
                    '''
                }
            }
            post {
                always {
                    sh 'docker-compose -f docker-compose.yml down || true'
                }
            }
        }

        stage('Push to Registry') {
            when { branch 'main' }
            agent { label 'master' }
            steps {
                script {
                    withCredentials([usernamePassword(credentialsId: 'docker-hub', usernameVariable: 'DOCKER_USER', passwordVariable: 'DOCKER_PASS')]) {
                        sh '''
                        echo $DOCKER_PASS | docker login -u $DOCKER_USER --password-stdin
                        docker tag ${IMAGE_NAME}:${IMAGE_TAG} ${REGISTRY}/${DOCKER_USER}/${IMAGE_NAME}:${IMAGE_TAG}
                        docker push ${REGISTRY}/${DOCKER_USER}/${IMAGE_NAME}:${IMAGE_TAG}
                        '''
                    }
                }
            }
        }

        stage('Deploy') {
            when { branch 'develop' }
            agent { label 'master' }
            steps {
                echo "Deploying to development environment..."
                // Add your deployment logic here
            }
        }
    }

    post {
        always {
            cleanWs()
        }
        failure {
            echo "Build failed! Check logs."
        }
        success {
            echo "Build succeeded!"
        }
    }
}