package com.pocketree.app

import org.junit.Assert.assertEquals
import org.junit.Test

class UserStateTest {
    @Test
    fun defaults_areSane() {
        val state = UserState()

        assertEquals("User", state.username)
        assertEquals(0, state.totalCoins)
        assertEquals(1, state.currentLevelID)
        assertEquals("Seedling", state.levelName)
        assertEquals("", state.levelImageUrl)
        assertEquals("", state.profileImageUrl)
        assertEquals(false, state.isWithered)
        assertEquals(100, state.plantHealthPercent)
    }
}
