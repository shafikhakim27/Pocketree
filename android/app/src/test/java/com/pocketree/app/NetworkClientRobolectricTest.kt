package com.pocketree.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RuntimeEnvironment
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(application = MyApplication::class, sdk = [21], manifest = Config.NONE)
class NetworkClientRobolectricTest {
    @Test
    fun token_roundTrip_persistsInPrefs() {
        val context = RuntimeEnvironment.getApplication()
        val prefs = context.getSharedPreferences("AppPrefs", android.content.Context.MODE_PRIVATE)
        prefs.edit().clear().apply()

        NetworkClient.setToken(context, "unit-token")
        val loaded = NetworkClient.loadToken(context)

        assertEquals("unit-token", loaded)
    }

    @Test
    fun userCache_roundTrip_persistsAndLoads() {
        val context = RuntimeEnvironment.getApplication()
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
        val context = RuntimeEnvironment.getApplication()
        val prefs = context.getSharedPreferences("AppPrefs", android.content.Context.MODE_PRIVATE)
        prefs.edit().clear().apply()

        val loaded = NetworkClient.loadUserCache(context)

        assertEquals(null, loaded)
    }

    @Test
    fun setToken_nullClearsToken() {
        val context = RuntimeEnvironment.getApplication()
        val prefs = context.getSharedPreferences("AppPrefs", android.content.Context.MODE_PRIVATE)
        prefs.edit().clear().apply()

        NetworkClient.setToken(context, "token")
        NetworkClient.setToken(context, null)

        assertEquals(null, NetworkClient.loadToken(context))
    }
}
