package com.pocketree.app

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mockito.kotlin.mock
import org.mockito.kotlin.whenever

class MockitoUnitTest {
    interface Clock {
        fun now(): Long
    }

    class Greeter(private val clock: Clock) {
        fun greeting(): String {
            return if (clock.now() < 12) "Good morning" else "Good evening"
        }
    }

    @Test
    fun greeting_usesClock() {
        val clock = mock<Clock>()
        whenever(clock.now()).thenReturn(9)

        val greeter = Greeter(clock)

        assertEquals("Good morning", greeter.greeting())
    }
}
