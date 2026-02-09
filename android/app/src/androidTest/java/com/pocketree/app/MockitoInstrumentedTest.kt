package com.pocketree.app

import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Test
import org.junit.runner.RunWith
import org.mockito.kotlin.mock
import org.mockito.kotlin.verify

@RunWith(AndroidJUnit4::class)
class MockitoInstrumentedTest {
    @Test
    fun mockito_runsOnDevice() {
        val runnable = mock<Runnable>()
        runnable.run()
        verify(runnable).run()
    }
}
