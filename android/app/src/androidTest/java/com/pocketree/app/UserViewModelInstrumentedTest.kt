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

@RunWith(AndroidJUnit4::class)
class UserViewModelInstrumentedTest {
    @Test
    fun updateUserData_updatesUserState() {
        val viewModel = UserViewModel()

        // Prevent network calls by ensuring tasks is not empty.
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

        val observer: Observer<UserState> = mock()
        viewModel.userState.observeForever(observer)

        try {
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
            viewModel.userState.removeObserver(observer)
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
}
