package com.pocketree.app

import android.app.Application
import android.content.Context

// trial
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