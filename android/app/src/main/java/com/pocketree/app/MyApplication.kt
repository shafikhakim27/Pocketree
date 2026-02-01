package com.pocketree.app

import android.app.Application
import android.content.Context

// creating a Global Access Point to set Context
// (Application Context lives as long as app process is alive)
class MyApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        instance = this
    }
    companion object {
        private lateinit var instance: MyApplication
        fun getContext(): Context = instance.applicationContext
    }
}