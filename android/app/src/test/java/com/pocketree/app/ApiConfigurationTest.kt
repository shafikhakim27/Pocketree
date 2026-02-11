package com.pocketree.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ApiConfigurationTest {
    @Test
    fun apiEndpoints_useBaseUrl() {
        assertTrue(ApiConfiguration.TASK_API_URL.startsWith(ApiConfiguration.BASE_URL))
        assertTrue(ApiConfiguration.USER_API_URL.startsWith(ApiConfiguration.BASE_URL))
    }
}
