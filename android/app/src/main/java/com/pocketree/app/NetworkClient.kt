package com.pocketree.app

import android.content.Context
import android.content.Intent
import com.google.gson.FieldNamingPolicy
import com.google.gson.GsonBuilder
import okhttp3.Interceptor
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Protocol
import okhttp3.Response
import okhttp3.ResponseBody.Companion.toResponseBody

// interceptor object for Json Web Token
// NetworkClient is "provider" of authenticated connection

object NetworkClient {
    var isMockMode: Boolean = false

    val okHttpClient = OkHttpClient.Builder()
        .addInterceptor { chain ->
            // add by chenyu, 2.11
            if (isMockMode) {
                val uri = chain.request().url.toUri().toString()
                val responseString = getMockResponse(uri)
                return@addInterceptor Response.Builder()
                    .code(200)
                    .message("Mock Success")
                    .request(chain.request())
                    .protocol(Protocol.HTTP_1_1)
                    .body(responseString.toResponseBody("application/json".toMediaType()))
                    .addHeader("content-type", "application/json")
                    .build()
            }

            // Standard interceptor to add the current token
            val original = chain.request()
            val token = loadToken(MyApplication.getContext())

            val requestBuilder = original.newBuilder()
            if (!token.isNullOrEmpty() && token != "no_token") {
                requestBuilder.addHeader("Authorization", "Bearer $token")
            }
            chain.proceed(requestBuilder.build())
        }
        .authenticator { _, response ->
            if (response.priorResponse != null) {
                triggerLogout()
                // logout if we already tried this request once and it still failed
                return@authenticator null
            }

            val token = loadToken(MyApplication.getContext())
            if (token.isNullOrEmpty() || token == "no_token") {
                triggerLogout()
                return@authenticator null
            }

            // Token exists but auth failed, might be expired
            triggerLogout()
            null
        }
        .build()

    val gson = GsonBuilder()
        .setFieldNamingPolicy(FieldNamingPolicy.IDENTITY) // Or check if your backend is sending camelCase
        .create()

    fun setToken(context:Context, token: String?) {
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        prefs.edit().putString("JWT_TOKEN", token).apply()
    }

    fun loadToken(context:Context):String? {
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        return prefs.getString("JWT_TOKEN", null)
    }

    // to keep user logged in (and for user info to be "saved" and displayed upon re-launching app)
    fun saveUserCache(context:Context, user:User) {
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        val userJson = gson.toJson(user)
        prefs.edit().putString("LAST_USER_DATA", userJson).apply()
    }

    fun loadUserCache(context:Context): User? {
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        val json = prefs.getString("LAST_USER_DATA", null) ?: return null
        return gson.fromJson(json, User::class.java)
    }

    private fun triggerLogout(){
        val context = MyApplication.getContext()
        val logoutIntent = Intent("ACTION_LOGOUT")
        logoutIntent.setPackage(context.packageName) // Safety for Android 14+
        context.sendBroadcast(logoutIntent)
    }

    private fun getMockResponse(uri: String): String {
        return when {
            uri.contains("LoginApi", ignoreCase = true) -> """
                {
                    "token": "mock-token-123-xyz",
                    "user": {
                        "username": "ecotester",
                        "email": "tester@pocketree.com",
                        "totalCoins": 150,
                        "currentLevelId": 1,
                        "levelName": "Seedling",
                        "levelImageUrl": "images/levels/seedling.png",
                        "profileImageUrl": "images/default-user.jpg",
                        "isWithered": false,
                        "plantHealthPercent": 80
                    }
                }
            """.trimIndent()

            uri.contains("GetUserProfileApi", ignoreCase = true) -> """
                {
                    "username": "ecotester",
                    "email": "tester@pocketree.com",
                    "totalCoins": 150,
                    "currentLevelId": 1,
                    "levelName": "Seedling",
                    "levelImageUrl": "images/levels/seedling.png",
                    "profileImageUrl": "images/default-user.jpg",
                    "isWithered": false,
                    "plantHealthPercent": 80
                }
            """.trimIndent()

            uri.contains("GetDailyTasksApi", ignoreCase = true) -> """
                [
                  {
                    "taskID": 1,
                    "description": "Recycle a plastic bottle (Mock)",
                    "difficulty": "Easy",
                    "coinReward": 10,
                    "isCompleted": false,
                    "isPassed": false,
                    "requiresEvidence": false,
                    "category": "Recycling"
                  },
                  {
                    "taskID": 2,
                    "description": "Use a reusable bag (Mock)",
                    "difficulty": "Normal",
                    "coinReward": 20,
                    "isCompleted": false,
                    "isPassed": false,
                    "requiresEvidence": true,
                    "category": "Reduce"
                  }
                ]
            """.trimIndent()

            uri.contains("RecordTaskCompletionApi", ignoreCase = true) -> """
                {
                  "success": true,
                  "status": "Completed",
                  "levelUp": false,
                  "newCoins": 160,
                  "newLevel": 1,
                  "isWithered": false,
                  "newLevelName": "Seedling",
                  "plantHealthPercent": 100
                }
            """.trimIndent()

            uri.contains("GetLatestBadgesApi", ignoreCase = true) -> """
                [
                  {
                    "badgeID": 1,
                    "badgeName": "Eco Starter",
                    "badgeDescription": "First step to saving the world",
                    "badgeImageURL": "images/badges/tree_starter.png",
                    "dateEarned": "2026-02-10T10:00:00Z"
                  }
                ]
            """.trimIndent()

            uri.contains("RedeemSkinApi", ignoreCase = true) -> """
                { "newCoins": 50 }
            """.trimIndent()

            uri.contains("GetSkinsShopApi", ignoreCase = true) -> """
                [
                  {
                    "skinID": 1,
                    "skinName": "Animals",
                    "skinPrice": 50,
                    "imageURL": "images/redeem/redeem_skin_animals.png",
                    "isRedeemed": false,
                    "isEquipped": false
                  }
                ]
            """.trimIndent()

            uri.contains("LogoutApi", ignoreCase = true) -> "Logged out successfully."

            else -> "{}"
        }
    }
}
