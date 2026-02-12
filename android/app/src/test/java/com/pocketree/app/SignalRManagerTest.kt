package com.pocketree.app

import com.microsoft.signalr.HubConnection
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Test

class SignalRManagerTest {
    @Test
    fun init_thenStopConnection_updatesInternalHubConnectionState() {
        SignalRManager.stopConnection()
        assertNull(getHubConnection())

        val viewModel = UserViewModel()
        SignalRManager.init("test-token", viewModel)

        val afterInit = getHubConnection()
        assertNotNull(afterInit)

        SignalRManager.stopConnection()
        assertNull(getHubConnection())
    }

    @Test
    fun init_calledTwice_reusesSameConnectionInstance() {
        SignalRManager.stopConnection()

        val viewModel = UserViewModel()
        SignalRManager.init("test-token", viewModel)
        val first = getHubConnection()

        SignalRManager.init("test-token", viewModel)
        val second = getHubConnection()

        assertNotNull(first)
        assertNotNull(second)
        assertSame(first, second)

        SignalRManager.stopConnection()
    }

    private fun getHubConnection(): HubConnection? {
        val field = SignalRManager::class.java.getDeclaredField("hubConnection")
        field.isAccessible = true
        return field.get(SignalRManager) as HubConnection?
    }
}
