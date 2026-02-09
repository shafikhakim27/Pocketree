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
    fun resolveImageUrl_keepsAbsoluteUrl() {
        val absolute = "https://cdn.example.com/images/tree.png"
        val resolved = ApiConfiguration.resolveImageUrl(absolute)
        assertEquals(absolute, resolved)
    }

    @Test
    fun resolveImageUrl_keepsRelativeWithoutTilde() {
        val relative = "images/trees/tree.png"
        val resolved = ApiConfiguration.resolveImageUrl(relative)
        assertEquals(relative, resolved)
    }

    @Test
    fun resolveImageUrl_keepsAbsoluteHttpUrl() {
        val absolute = "http://example.com/images/tree.png"
        val resolved = ApiConfiguration.resolveImageUrl(absolute)
        assertEquals(absolute, resolved)
    }

    @Test
    fun resolveImageUrl_handlesTildeRootOnly() {
        val resolved = ApiConfiguration.resolveImageUrl("~/")
        assertTrue(resolved.startsWith(ApiConfiguration.IMAGE_BASE_URL))
    }

    @Test
    fun apiEndpoints_useBaseUrl() {
        assertTrue(ApiConfiguration.TASK_API_URL.startsWith(ApiConfiguration.BASE_URL))
        assertTrue(ApiConfiguration.USER_API_URL.startsWith(ApiConfiguration.BASE_URL))
    }
}
