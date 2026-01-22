pipeline {
    agent none
    stages {
        stage('Parallel Demo') {
            parallel {
                stage('One') {
                    steps {
                        echo "Hello from One"
                    }
                }
                stage('Two') {
                    steps {
                        echo "Hello from Two"
                    }
                }
            }
        }
    }
}