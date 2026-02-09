package com.pocketree.app

import androidx.lifecycle.Observer
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.mockito.kotlin.argThat
import org.mockito.kotlin.mock
import org.mockito.kotlin.timeout
import org.mockito.kotlin.verify
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

@RunWith(AndroidJUnit4::class)
class UserViewModelInstrumentedTest {
    @Test
    fun updateUserData_updatesUserState() {
        val viewModel = UserViewModel()

        // Prevent network calls by ensuring tasks is not empty.
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            viewModel.tasks.value = listOf(
                Task(
                    taskID = 1,
                    description = "Sample",
                    isCompleted = false,
                    isPassed = false,
                    difficulty = "Easy",
                    coinReward = 10,
                    requiresEvidence = false,
                    keyword = null,
                    category = null
                )
            )
        }

        val observer: Observer<UserState> = mock()
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            viewModel.userState.observeForever(observer)
        }

        try {
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                viewModel.updateUserData(
                    username = "ecotester",
                    totalCoins = 0,
                    currentLevelId = 1,
                    levelName = "Seedling",
                    isWithered = false,
                    levelImageUrl = "images/levels/seedling.png",
                    profileImageUrl = "",
                    plantHealthPercent = 100
                )
            }

            verify(observer, timeout(1000)).onChanged(argThat {
                username == "ecotester" &&
                    totalCoins == 0 &&
                    currentLevelID == 1 &&
                    levelName == "Seedling" &&
                    levelImageUrl == "images/levels/seedling.png" &&
                    profileImageUrl == "" &&
                    isWithered == false &&
                    plantHealthPercent == 100
            })
        } finally {
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                viewModel.userState.removeObserver(observer)
            }
        }
    }

    @Test
    fun performLocalCleanup_clearsPrefsAndResetsState() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val viewModel = UserViewModel()

        NetworkClient.setToken(context, "token")
        NetworkClient.saveUserCache(
            context,
            User(
                username = "tester",
                totalCoins = 0,
                currentLevelId = 1,
                levelName = "Seedling",
                levelImageUrl = "",
                profileImageUrl = "",
                isWithered = false,
                plantHealthPercent = 100
            )
        )

        viewModel.performLocalCleanup(context)

        val prefs = context.getSharedPreferences("AppPrefs", android.content.Context.MODE_PRIVATE)
        assertEquals(null, prefs.getString("JWT_TOKEN", null))
        assertEquals(null, prefs.getString("LAST_USER_DATA", null))
    }

    @Test
    fun performLocalCleanup_resetsLiveData() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val viewModel = UserViewModel()

        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            viewModel.tasks.value = listOf(
                Task(
                    taskID = 1,
                    description = "Sample",
                    isCompleted = true,
                    isPassed = false,
                    difficulty = "Easy",
                    coinReward = 10,
                    requiresEvidence = false,
                    keyword = null,
                    category = null
                )
            )

            viewModel.updateUserData(
                username = "tester",
                totalCoins = 99,
                currentLevelId = 3,
                levelName = "Mighty Oak",
                isWithered = true,
                levelImageUrl = "images/levels/tree.png",
                profileImageUrl = "images/users/test.png",
                plantHealthPercent = 10
            )
        }

        val latch = CountDownLatch(1)
        val observer: Observer<UserState> = Observer { state ->
            if (state == UserState()) {
                latch.countDown()
            }
        }

        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            viewModel.userState.observeForever(observer)
        }

        try {
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                viewModel.performLocalCleanup(context)
            }
            InstrumentationRegistry.getInstrumentation().waitForIdleSync()
            val completed = latch.await(1, TimeUnit.SECONDS)
            assertEquals(true, completed)
            assertEquals(true, viewModel.tasks.value?.isEmpty() == true)
            assertEquals(true, viewModel.earnedBadges.value?.isEmpty() == true)
        } finally {
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                viewModel.userState.removeObserver(observer)
            }
        }
    }
}
