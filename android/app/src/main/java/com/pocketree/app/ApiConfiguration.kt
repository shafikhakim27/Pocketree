package com.pocketree.app

/**
 * Central configuration for API and resource URLs
 *
 * DEPLOYMENT INSTRUCTIONS:
 * 1. For local testing: Use LOCAL_MODE = true
 * 2. For production: Set LOCAL_MODE = false and update PRODUCTION_BASE_URL
 */
object ApiConfiguration {
    // Toggle between local and production mode
    private const val LOCAL_MODE = false

    // Local development URLs (Android Emulator)
    private const val LOCAL_BASE_URL = "http://10.0.2.2:5042"

    // Production URL (update this when deploying)
    private const val PRODUCTION_BASE_URL = "https://pocketree-api.azurewebsites.net"

    // Automatically select the correct base URL
    val BASE_URL = if (LOCAL_MODE) LOCAL_BASE_URL else PRODUCTION_BASE_URL

    // API Endpoints
    val TASK_API_URL = "$BASE_URL/api/Task"
    val USER_API_URL = "$BASE_URL/api/User"
}
