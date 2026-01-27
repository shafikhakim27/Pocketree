pipeline {
    agent none

    options {
        ansiColor('xterm')
        timestamps()
        timeout(time: 1, unit: 'HOURS')
    }

    environment {
        // Configuration
        DOTNET_SDK = '9.0'
        REGISTRY = 'docker.io'
        IMAGE_NAME = 'pocketree-api'
        IMAGE_TAG = "${BUILD_NUMBER}"
        
        // SonarQube (Optional)
        SONAR_TOKEN = credentials('sonar-token')
        SONAR_HOST_URL = 'http://sonarqube:9000'
    }

    stages {
        stage('Setup') {
            agent any
            steps {
                script {
                    echo "Setting up build environment..."
                    sh 'docker --version'
                }
            }
        }

        stage('Parallel Build') {
            parallel {
                
                // 1. BACKEND (.NET)
                stage('Backend Build') {
                    agent {
                        docker {
                            image "mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK}"
                            args '--network fixed-pocketree-network -u 0:0 --rm'
                            reuseNode true
                        }
                    }
                    steps {
                        checkout scm
                        script {
                            sh '''
                            dotnet restore Pocketree.sln
                            dotnet build Pocketree.sln -c Release --no-restore
                            '''
                        }
                    }
                }

                // 2. ML SERVICE (Python/CLIP)
                stage('ML Service Build') {
                    agent {
                        // We use a base python image just to verify syntax/imports
                        docker {
                            image 'python:3.9-slim' 
                            args '--network fixed-pocketree-network -u 0:0 --rm'
                            reuseNode true
                        }
                    }
                    steps {
                        checkout scm
                        dir('ml-service') {
                            sh '''
                            pip install --upgrade pip
                            # Install minimal deps to check syntax (skipping heavy torch for speed in this check)
                            pip install flask mysql-connector-python
                            
                            # Verify the script parses correctly (Syntax Check)
                            python -m py_compile CLIPModel-donotmerge.py
                            echo "Syntax check passed!"
                            '''
                        }
                    }
                }
            }
        }

        stage('Build Docker Images') {
            agent any
            steps {
                checkout scm
                script {
                    echo "Building Docker Images..."
                    sh '''
                    # Build Backend
                    docker build \
                        -f src/Pocketree.Api/Dockerfile \
                        -t ${IMAGE_NAME}:${IMAGE_TAG} \
                        -t ${IMAGE_NAME}:latest \
                        .
                    
                    # Build ML Service (This installs the heavy PyTorch libs)
                    docker build \
                        -f ml-service/Dockerfile \
                        -t pocketree-ml:${IMAGE_TAG} \
                        -t pocketree-ml:latest \
                        ml-service/
                    '''
                }
            }
        }

        stage('Smoke Test') {
            agent any
            steps {
                script {
                    echo "Verifying ML Container starts..."
                    // We simply check if python version prints, ensuring the image is valid.
                    sh 'docker run --rm --entrypoint python pocketree-ml:latest --version'
                }
            }
        }

        stage('Deploy (Develop)') {
            when { branch 'develop' }
            agent any
            steps {
                echo "Deploying to development..."
                script {
                    // Rebuilds and restarts containers with the new code
                    sh 'docker compose -f docker-compose.yml up -d --build'
                }
            }
        }
    }

    post {
        always {
            cleanWs()
        }
        success {
            echo "Pipeline succeeded!"
        }
        failure {
            echo "Pipeline failed."
        }
    }
}