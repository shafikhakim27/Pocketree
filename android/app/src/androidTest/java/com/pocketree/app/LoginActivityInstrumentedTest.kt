package com.pocketree.app

import androidx.lifecycle.Lifecycle
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class LoginActivityInstrumentedTest {
    @Test
    fun whenTokenExists_loginActivityFinishes() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        NetworkClient.setToken(context, "test-token")

        val scenario = ActivityScenario.launch(LoginActivity::class.java)
        InstrumentationRegistry.getInstrumentation().waitForIdleSync()

        val state = scenario.state
        assertTrue(
            "LoginActivity should finish when token exists (state=$state)",
            state == Lifecycle.State.DESTROYED || state == Lifecycle.State.STOPPED || state == Lifecycle.State.CREATED
        )

        scenario.close()
    }
}
