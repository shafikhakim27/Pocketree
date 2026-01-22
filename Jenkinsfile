pipeline {
    agent none

    stages {
        stage('Parallel Build & Checks') {
            parallel {
                stage('Backend Debug') {
                    agent { docker { image 'mcr.microsoft.com/dotnet/sdk:8.0' } }
                    steps {
                        echo "=== Backend Stage Debug ==="
                        sh 'pwd'
                        sh 'ls -R'
                    }
                }

                stage('Android Debug') {
                    agent {
                        docker {
                            image 'mobiledevops/android-sdk-image:latest'
                            args '-u root'
                        }
                    }
                    steps {
                        echo "=== Android Stage Debug ==="
                        sh 'pwd'
                        sh 'ls -R'
                    }
                }

                stage('Database Debug') {
                    agent { docker { image 'mysql:8.0' } }
                    steps {
                        echo "=== Database Stage Debug ==="
                        sh 'pwd'
                        sh 'ls -R'
                        sh 'ping -c 2 pocketree-db || true'
                    }
                }
            }
        }
    }
}