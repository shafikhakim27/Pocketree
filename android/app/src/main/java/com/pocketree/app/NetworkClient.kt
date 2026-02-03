package com.pocketree.app

import android.content.Context
import android.content.Intent
import android.util.Log
import com.google.gson.FieldNamingPolicy
import com.google.gson.GsonBuilder
import okhttp3.Interceptor
import okhttp3.OkHttpClient

// interceptor object for Json Web Token

object NetworkClient {

    val okHttpClient = OkHttpClient.Builder()
        .addInterceptor { chain ->
            // Standard interceptor to add the current token
            val original = chain.request()
            // val token = loadToken(context)
            val token = loadToken(MyApplication.getContext())

            android.util.Log.d("NETWORK", "Request URL: ${original.url}")
            android.util.Log.d("NETWORK", "Token: ${token?.take(20)}...")

            val requestBuilder = original.newBuilder()
            if (!token.isNullOrEmpty() && token != "no_token") {
                requestBuilder.addHeader("Authorization", "Bearer $token")
                Log.d("NETWORK_CLIENT", "Token added (first 20 chars): ${token.take(20)}...")
            } else {
                Log.w("NETWORK_CLIENT", "No valid token available")
            }

            val request = requestBuilder.build()
            val response = chain.proceed(request)
            // chain.proceed(requestBuilder.build())

            Log.d("NETWORK_CLIENT", "Response Code: ${response.code}")
            if(!response.isSuccessful) {
                Log.e("NETWORK_CLIENT", "Request failed: ${response.message}")
            }
            response
        }
        .authenticator { _, response ->
            if (response.priorResponse != null) {
                triggerLogout()
                // logout if we already tried this request once and it still failed
                return@authenticator null
            }

            val token = loadToken(MyApplication.getContext())
            if (token.isNullOrEmpty() || token == "no_token") {
                Log.e("NETWORK_CLIENT", "No token available for retry, logging out")
                triggerLogout()
                return@authenticator null
            }

            // Token exists but auth failed, might be expired
            Log.w("NETWORK_CLIENT", "Auth failed with code ${response.code}, triggering logout")
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

        if (token != null) {
            Log.d("NETWORK_CLIENT", "Token saved (first 20 chars): ${token.take(20)}...")
        } else {
            Log.d("NETWORK_CLIENT", "Token cleared")
        }
    }

    fun loadToken(context:Context):String? {
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        //return prefs.getString("JWT_TOKEN", null)

        // for debug only
        val token = prefs.getString("JWT_TOKEN", null)

        if (token != null) {
            Log.d("NETWORK_CLIENT", "Token loaded (first 20 chars): ${token.take(20)}...")
        } else {
            Log.w("NETWORK_CLIENT", "No token found in SharedPreferences")
        }

        return token
        // end of debug
    }

    // to keep user logged in (and for user info to be "saved" and displayed upon re-launching app)
    fun saveUserCache(context:Context, user:User) {
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        val userJson = gson.toJson(user)
        prefs.edit().putString("LAST_USER_DATA", userJson).apply()
        Log.d("NETWORK_CLIENT", "User cache saved for: ${user.username}")
    }

    fun loadUserCache(context:Context): User? {
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        val json = prefs.getString("LAST_USER_DATA", null) ?: return null
        // for debug
        val user = gson.fromJson(json, User::class.java)
        Log.d("NETWORK_CLIENT", "User cache loaded for: ${user?.username}")
        // for debug
        return gson.fromJson(json, User::class.java)
    }

    private fun triggerLogout(){
        Log.d("NETWORK_CLIENT", "Triggering logout broadcast")
        val context = MyApplication.getContext()
        val logoutIntent = Intent("ACTION_LOGOUT")
        logoutIntent.setPackage(context.packageName) // Safety for Android 14+
        context.sendBroadcast(logoutIntent)
    }
}
