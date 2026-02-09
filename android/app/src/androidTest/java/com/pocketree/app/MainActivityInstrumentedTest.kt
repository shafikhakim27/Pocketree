package com.pocketree.app

import androidx.lifecycle.Lifecycle
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class MainActivityInstrumentedTest {
    @Test
    fun whenTokenMissing_mainActivityFinishes() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        NetworkClient.setToken(context, null)

        val scenario = ActivityScenario.launch(MainActivity::class.java)
        InstrumentationRegistry.getInstrumentation().waitForIdleSync()

        val state = scenario.state
        assertTrue(
            "MainActivity should finish when token missing (state=$state)",
            state == Lifecycle.State.DESTROYED || state == Lifecycle.State.CREATED
        )

        scenario.close()
    }
}
