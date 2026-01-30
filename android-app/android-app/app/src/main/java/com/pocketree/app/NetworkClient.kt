package com.pocketree.app

import android.content.Context
import android.content.Intent
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

            if (loadToken(MyApplication.getContext()) == null) {
                triggerLogout()
            }
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
}
