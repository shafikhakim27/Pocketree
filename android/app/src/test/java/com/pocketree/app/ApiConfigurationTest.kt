package com.pocketree.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ApiConfigurationTest {

    @Test
    fun resolveImageUrl_returnsEmptyForNullOrEmpty() {
        assertEquals("", ApiConfiguration.resolveImageUrl(null))
        assertEquals("", ApiConfiguration.resolveImageUrl(""))
    }

    @Test
    fun resolveImageUrl_replacesTildePrefix() {
        val resolved = ApiConfiguration.resolveImageUrl("~/images/trees/tree.png")
        assertTrue(resolved.startsWith(ApiConfiguration.IMAGE_BASE_URL))
        assertTrue(resolved.contains("/images/trees/tree.png"))
    }

    @Test
    fun apiEndpoints_useBaseUrl() {
        assertTrue(ApiConfiguration.TASK_API_URL.startsWith(ApiConfiguration.BASE_URL))
        assertTrue(ApiConfiguration.USER_API_URL.startsWith(ApiConfiguration.BASE_URL))
    }
}
