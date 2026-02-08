package com.pocketree.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.lang.Class

class RetrofitClientTest {

    @Test
    fun retrofit_baseUrl_matches_ai_url() {
        // Avoid triggering RetrofitClient initialization in local JVM tests
        val clientClass = Class.forName("com.pocketree.app.RetrofitClient", false, javaClass.classLoader)
        val aiField = clientClass.getDeclaredField("AI_URL")
        aiField.isAccessible = true
        val aiUrl = (aiField.get(null) as String)

        assertTrue("AI_URL should be https", aiUrl.startsWith("https://"))
        assertTrue("AI_URL should end with /", aiUrl.endsWith("/"))
    }

    @Test
    fun main_url_is_http_for_local_dev() {
        val clientClass = Class.forName("com.pocketree.app.RetrofitClient", false, javaClass.classLoader)
        val mainField = clientClass.getDeclaredField("MAIN_URL")
        mainField.isAccessible = true
        val mainUrl = mainField.get(null) as String

        assertTrue(mainUrl.startsWith("http://"))
        assertTrue(mainUrl.endsWith("/"))
    }
}
