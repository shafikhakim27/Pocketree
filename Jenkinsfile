pipeline {
    agent {
        docker { 
            image 'mcr.microsoft.com/dotnet/sdk:8.0' 
            args '-u root' 
        }
    }
    stages {
        stage('Checkout') {
            steps { checkout scm }
        }
        stage('Build') {
            steps {
                sh 'dotnet build Pocketree.sln'
            }
        }
        stage('Test') {
            steps {
                // This runs the tests and produces a report
                sh 'dotnet test Pocketree.sln --no-build --logger "trx;LogFileName=test_results.xml"'
            }
        }
    }
}