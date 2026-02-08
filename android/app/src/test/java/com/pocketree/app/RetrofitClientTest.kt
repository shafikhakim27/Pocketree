package com.pocketree.app

import org.junit.Assert.assertTrue
import org.junit.Test

class RetrofitClientTest {

    @Test
    fun base_url_is_http_or_https() {
        val baseUrl = ApiConfiguration.BASE_URL
        assertTrue(baseUrl.startsWith("http://") || baseUrl.startsWith("https://"))
    }

    @Test
    fun main_url_is_http_for_local_dev() {
        val baseUrl = ApiConfiguration.BASE_URL
        // If LOCAL_MODE is true, this should be http://10.0.2.2:5042
        if (baseUrl.contains("10.0.2.2")) {
            assertTrue(baseUrl.startsWith("http://"))
        } else {
            assertTrue(baseUrl.startsWith("https://"))
        }
    }
}
