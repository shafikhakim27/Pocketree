package com.pocketree.app

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import androidx.core.content.ContextCompat
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test
import org.junit.runner.RunWith
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

@RunWith(AndroidJUnit4::class)
class NetworkClientInstrumentedTest {
    @Test
    fun token_roundTrip_persistsInPrefs() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val prefs = context.getSharedPreferences("AppPrefs", android.content.Context.MODE_PRIVATE)
        prefs.edit().clear().apply()

        NetworkClient.setToken(context, "unit-token")
        val loaded = NetworkClient.loadToken(context)

        assertEquals("unit-token", loaded)
    }

    @Test
    fun userCache_roundTrip_persistsAndLoads() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val prefs = context.getSharedPreferences("AppPrefs", android.content.Context.MODE_PRIVATE)
        prefs.edit().clear().apply()

        val user = User(
            username = "tester",
            totalCoins = 42,
            currentLevelId = 2,
            levelName = "Sapling",
            levelImageUrl = "images/levels/sapling.png",
            profileImageUrl = "images/users/test.png",
            isWithered = false,
            plantHealthPercent = 80
        )

        NetworkClient.saveUserCache(context, user)
        val loaded = NetworkClient.loadUserCache(context)

        assertNotNull(loaded)
        assertEquals(user.username, loaded?.username)
        assertEquals(user.totalCoins, loaded?.totalCoins)
        assertEquals(user.currentLevelId, loaded?.currentLevelId)
        assertEquals(user.levelName, loaded?.levelName)
        assertEquals(user.levelImageUrl, loaded?.levelImageUrl)
        assertEquals(user.profileImageUrl, loaded?.profileImageUrl)
        assertEquals(user.isWithered, loaded?.isWithered)
        assertEquals(user.plantHealthPercent, loaded?.plantHealthPercent)
    }

    @Test
    fun userCache_returnsNullWhenMissing() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val prefs = context.getSharedPreferences("AppPrefs", android.content.Context.MODE_PRIVATE)
        prefs.edit().clear().apply()

        val loaded = NetworkClient.loadUserCache(context)
        assertEquals(null, loaded)
    }

    @Test
    fun setToken_nullClearsToken() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val prefs = context.getSharedPreferences("AppPrefs", android.content.Context.MODE_PRIVATE)
        prefs.edit().clear().apply()

        NetworkClient.setToken(context, "token")
        NetworkClient.setToken(context, null)

        assertEquals(null, NetworkClient.loadToken(context))
    }

    @Test
    fun triggerLogout_sendsBroadcast() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val latch = CountDownLatch(1)

        val receiver = object : BroadcastReceiver() {
            override fun onReceive(ctx: Context, intent: Intent) {
                if (intent.action == "ACTION_LOGOUT") {
                    latch.countDown()
                }
            }
        }

        val filter = IntentFilter("ACTION_LOGOUT")
        ContextCompat.registerReceiver(
            context,
            receiver,
            filter,
            ContextCompat.RECEIVER_NOT_EXPORTED
        )

        try {
            val method = NetworkClient::class.java.getDeclaredMethod("triggerLogout")
            method.isAccessible = true
            method.invoke(NetworkClient)

            val received = latch.await(2, TimeUnit.SECONDS)
            assertEquals(true, received)
        } finally {
            context.unregisterReceiver(receiver)
        }
    }
}
