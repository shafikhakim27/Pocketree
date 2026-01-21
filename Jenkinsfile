pipeline {
    agent none 

    stages {
        parallel {
            
            // --- STAGE 1: BACKEND ---
            stage('Backend Build & Security') {
                agent {
                    docker { image 'mcr.microsoft.com/dotnet/sdk:8.0' }
                }
                steps {
                    sh 'chmod -R 777 .'
                    
                    // 1. Check for security vulnerabilities
                    sh 'dotnet list Pocketree.sln package --vulnerable --include-transitive'
                    
                    // 2. Build & Test
                    sh 'dotnet build Pocketree.sln'
                    // Run tests and save results to a file named 'TestResults.xml'
                    sh 'dotnet test Pocketree.sln --no-build --logger "trx;LogFileName=TestResults.xml"'
                }
            }

            // --- STAGE 2: ANDROID ---
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
                        
                        // 1. Run Logic Tests
                        sh './gradlew testDebugUnitTest'
                        
                        // 2. Run Lint (Code Quality Check)
                        sh './gradlew lintDebug'
                        
                        // 3. Build the actual APK for download
                        sh './gradlew assembleDebug'
                    }
                }
            }

            // --- STAGE 3: DATABASE ---
            stage('Database Check') {
                agent {
                    docker { 
                        image 'mysql:8.0' 
                        args '-e MYSQL_ROOT_PASSWORD=testpass' 
                    }
                }
                steps {
                    sleep 10
                    sh 'mysql -h 127.0.0.1 -u root -ptestpass < init.sql'
                }
            }
        }
    }

    // --- NEW SECTION: POST ACTIONS ---
    post {
        always {
            // 1. Save Backend Test Results (Charts!)
            mstest testResultsFile: '**/TestResults.xml', keepLongStdio: true
            
            // 2. Save Android APK (So you can download it)
            archiveArtifacts artifacts: '**/*.apk', allowEmptyArchive: true
            
            // 3. Save Android Lint Report (Quality Report)
            archiveArtifacts artifacts: '**/lint-results-debug.xml', allowEmptyArchive: true
        }
    }
}