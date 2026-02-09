package com.pocketree.app

import okhttp3.Request
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RuntimeEnvironment
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(application = MyApplication::class, sdk = [21], manifest = Config.NONE)
class NetworkClientInterceptorTest {
    @Test
    fun addsAuthorizationHeader_whenTokenExists() {
        val context = RuntimeEnvironment.getApplication()
        NetworkClient.setToken(context, "token-123")

        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(200))
        server.start()

        val request = Request.Builder()
            .url(server.url("/test"))
            .build()

        try {
            NetworkClient.okHttpClient.newCall(request).execute().close()
            val recorded = server.takeRequest()
            assertEquals("Bearer token-123", recorded.getHeader("Authorization"))
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun doesNotAddAuthorizationHeader_whenTokenMissing() {
        val context = RuntimeEnvironment.getApplication()
        NetworkClient.setToken(context, null)

        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(200))
        server.start()

        val request = Request.Builder()
            .url(server.url("/test"))
            .build()

        try {
            NetworkClient.okHttpClient.newCall(request).execute().close()
            val recorded = server.takeRequest()
            assertNull(recorded.getHeader("Authorization"))
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun doesNotAddAuthorizationHeader_whenTokenNoToken() {
        val context = RuntimeEnvironment.getApplication()
        NetworkClient.setToken(context, "no_token")

        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(200))
        server.start()

        val request = Request.Builder()
            .url(server.url("/test"))
            .build()

        try {
            NetworkClient.okHttpClient.newCall(request).execute().close()
            val recorded = server.takeRequest()
            assertNull(recorded.getHeader("Authorization"))
        } finally {
            server.shutdown()
        }
    }
}
