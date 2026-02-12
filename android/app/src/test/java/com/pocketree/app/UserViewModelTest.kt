package com.pocketree.app

import androidx.arch.core.executor.testing.InstantTaskExecutorRule
import androidx.lifecycle.Observer
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.SocketPolicy
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RuntimeEnvironment
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

@RunWith(RobolectricTestRunner::class)
@Config(application = MyApplication::class, sdk = [21], manifest = Config.NONE)
class UserViewModelTest {
    @get:Rule
    val instantExecutorRule = InstantTaskExecutorRule()

    @Test
    fun fetchDailyTasks_postsTasks() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody(
                    """
                    [
                      {
                        "taskID": 1,
                        "description": "Recycle bottles",
                        "isCompleted": false,
                        "isPassed": false,
                        "difficulty": "Easy",
                        "coinReward": 5,
                        "requiresEvidence": false,
                        "keyword": null,
                        "category": "Recycling"
                      }
                    ]
                    """.trimIndent()
                )
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<List<Task>?> { tasks ->
            if (!tasks.isNullOrEmpty() && tasks[0].taskID == 1) {
                latch.countDown()
            }
        }
        viewModel.tasks.observeForever(observer)

        try {
            viewModel.fetchDailyTasks()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected tasks to be posted", completed)
            assertEquals(1, viewModel.tasks.value?.size)
            assertEquals("Recycle bottles", viewModel.tasks.value?.first()?.description)
        } finally {
            viewModel.tasks.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun fetchLatestBadges_postsBadges() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody(
                    """
                    [
                      {
                        "badgeID": 1,
                        "badgeName": "Tree Starter",
                        "badgeDescription": "Reach Level 2",
                        "badgeImageURL": "images/badges/tree_starter.png",
                        "dateEarned": "2026-02-01",
                        "criteriaType": "LevelUp",
                        "requiredDifficulty": "Any",
                        "requiredCount": 2
                      }
                    ]
                    """.trimIndent()
                )
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<List<Badge>> { badges ->
            if (badges.isNotEmpty() && badges[0].badgeName == "Tree Starter") {
                latch.countDown()
            }
        }
        viewModel.earnedBadges.observeForever(observer)

        try {
            viewModel.fetchLatestBadges()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected badges to be posted", completed)
            assertEquals(1, viewModel.earnedBadges.value?.size)
        } finally {
            viewModel.earnedBadges.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun fetchDailyTasks_badJson_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody("{not-json")
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Parsing error") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.fetchDailyTasks()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected parsing error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun fetchDailyTasks_networkFailure_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setSocketPolicy(SocketPolicy.DISCONNECT_AT_START)
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Network error loading tasks") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.fetchDailyTasks()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected network error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun fetchLatestBadges_badJson_postsEmptyList() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody("{not-json")
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<List<Badge>> { badges ->
            if (badges.isEmpty()) {
                latch.countDown()
            }
        }
        viewModel.earnedBadges.observeForever(observer)

        try {
            viewModel.fetchLatestBadges()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected empty badges list", completed)
        } finally {
            viewModel.earnedBadges.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun fetchSkins_success_postsList() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody(
                    """
                    [
                      {
                        "skinID": 1,
                        "skinName": "Animals",
                        "skinPrice": 50,
                        "imageURL": "images/redeem/redeem_skin_animals.png",
                        "isRedeemed": true,
                        "isEquipped": false
                      }
                    ]
                    """.trimIndent()
                )
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<List<Skin>> { skins ->
            if (skins.isNotEmpty() && skins[0].skinName == "Animals") {
                latch.countDown()
            }
        }
        viewModel.skins.observeForever(observer)

        try {
            viewModel.fetchSkins()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected skins list", completed)
        } finally {
            viewModel.skins.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun fetchVouchers_success_postsList() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody(
                    """
                    [
                      {
                        "voucherID": 1,
                        "voucherName": "Voucher 1",
                        "description": "Test voucher",
                        "redemptionCode": "ABC123",
                        "isRedeemed": false
                      }
                    ]
                    """.trimIndent()
                )
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<List<Voucher>> { vouchers ->
            if (vouchers.isNotEmpty() && vouchers[0].voucherName == "Voucher 1") {
                latch.countDown()
            }
        }
        viewModel.vouchers.observeForever(observer)

        try {
            viewModel.fetchVouchers()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected vouchers list", completed)
        } finally {
            viewModel.vouchers.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun redeemSkin_success_updatesCoinsAndEmitsEvent() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody("""{"newCoins": 50}""")
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())
        viewModel.userState.value = UserState(totalCoins = 1)

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Skin redeemed successfully!") {
                latch.countDown()
            }
        }
        viewModel.redeemSkinSuccessEvent.observeForever(observer)

        try {
            viewModel.redeemSkin(1)
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected redeem skin event", completed)
            assertEquals(50, viewModel.userState.value?.totalCoins)
        } finally {
            viewModel.redeemSkinSuccessEvent.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun redeemVoucher_success_emitsEvent() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody("""{"ok": true}""")
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Voucher used successfully!") {
                latch.countDown()
            }
        }
        viewModel.redeemVoucherSuccessEvent.observeForever(observer)

        try {
            viewModel.redeemVoucher(1)
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected redeem voucher event", completed)
        } finally {
            viewModel.redeemVoucherSuccessEvent.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun logout_success_clearsStateAndEmitsEvent() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(200))
        server.start()

        val context = RuntimeEnvironment.getApplication()
        NetworkClient.setToken(context, "token")
        NetworkClient.saveUserCache(
            context,
            User(
                username = "tester",
                totalCoins = 10,
                currentLevelId = 1,
                levelName = "Seedling",
                levelImageUrl = "",
                profileImageUrl = "",
                isWithered = false,
                plantHealthPercent = 100
            )
        )

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<Boolean> { flag ->
            if (flag == true) {
                latch.countDown()
            }
        }
        viewModel.logoutSuccess.observeForever(observer)

        try {
            viewModel.logout()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected logout success event", completed)
            assertEquals(null, NetworkClient.loadToken(context))
        } finally {
            viewModel.logoutSuccess.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun submitTask_success_updatesState() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody(
                    """
                    {
                      "success": true,
                      "status": "Completed",
                      "levelUp": false,
                      "newCoins": 25,
                      "newLevel": 2,
                      "isWithered": false,
                      "newLevelName": "Sapling",
                      "plantHealthPercent": 90
                    }
                    """.trimIndent()
                )
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())
        viewModel.userState.value = UserState(totalCoins = 0)
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

        val latch = CountDownLatch(1)
        val observer = Observer<UserState> { state ->
            if (state.totalCoins == 25 && state.levelName == "Sapling") {
                latch.countDown()
            }
        }
        viewModel.userState.observeForever(observer)

        try {
            viewModel.submitTask(1, "Completed")
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected updated user state", completed)
        } finally {
            viewModel.userState.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun submitTask_serverError_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(500))
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Server error") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.submitTask(1, "Completed")
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected server error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun submitTask_networkFailure_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setSocketPolicy(SocketPolicy.DISCONNECT_AT_START)
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Network error.") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.submitTask(1, "Completed")
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected network error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun submitTask_levelUp_emitsLevelUpEvent() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody(
                    """
                    {
                      "success": true,
                      "status": "Completed",
                      "levelUp": true,
                      "newCoins": 100,
                      "newLevel": 2,
                      "isWithered": false,
                      "newLevelName": "Sapling",
                      "plantHealthPercent": 95
                    }
                    """.trimIndent()
                )
        )
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody(
                    """
                    [
                      {
                        "badgeID": 1,
                        "badgeName": "Tree Starter",
                        "badgeDescription": "Reach Level 2",
                        "badgeImageURL": "images/badges/tree_starter.png",
                        "dateEarned": "2026-02-01",
                        "criteriaType": "LevelUp",
                        "requiredDifficulty": "Any",
                        "requiredCount": 2
                      }
                    ]
                    """.trimIndent()
                )
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())
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

        val latch = CountDownLatch(1)
        val observer = Observer<Triple<String, String, String>> { triple ->
            if (triple.first == "Sapling" && triple.second == "Tree Starter") {
                latch.countDown()
            }
        }
        viewModel.levelUpEvent.observeForever(observer)

        try {
            viewModel.submitTask(1, "Completed")
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected level-up event", completed)
        } finally {
            viewModel.levelUpEvent.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun fetchSkins_serverError_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(500))
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Failed to load skins") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.fetchSkins()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected skins error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun fetchSkins_parseError_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody("{not-json")
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Error parsing skins") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.fetchSkins()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected skins parse error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun fetchVouchers_networkFailure_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(MockResponse().setSocketPolicy(SocketPolicy.DISCONNECT_AT_START))
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Network error fetching vouchers") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.fetchVouchers()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected vouchers error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun fetchVouchers_parseError_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody("{not-json")
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Error parsing vouchers") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.fetchVouchers()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected vouchers parse error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun redeemSkin_failure_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(400)
                .setBody("Not enough coins")
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Failed: Not enough coins") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.redeemSkin(1)
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected redeem skin error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun redeemVoucher_failure_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(400)
                .setBody("Already redeemed")
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "taskBaseUrl", server.url("/api/Task").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Failed: Already redeemed") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.redeemVoucher(1)
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected redeem voucher error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun logout_failure_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(401))
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Logout failed") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.logout()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected logout failure message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun logout_networkFailure_postsErrorMessage() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(MockResponse().setSocketPolicy(SocketPolicy.DISCONNECT_AT_START))
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Network failure") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.logout()
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected logout network failure message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun sendPasswordChangeRequest_success_setsPasswordUpdateSuccess() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(200).setBody("""{"ok":true}"""))
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<Boolean> { success ->
            if (success == true) {
                latch.countDown()
            }
        }
        viewModel.passwordUpdateSuccess.observeForever(observer)

        try {
            viewModel.sendPasswordChangeRequest("old", "new", "new")
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected password update success", completed)
        } finally {
            viewModel.passwordUpdateSuccess.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun sendPasswordChangeRequest_400IncorrectPassword_postsInvalidPassword() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(400)
                .setBody("Current password is incorrect")
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Invalid password") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.sendPasswordChangeRequest("old", "new", "new")
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected invalid password message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun sendPasswordChangeRequest_401_postsSessionExpired() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(401).setBody("Unauthorized"))
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Session expired") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.sendPasswordChangeRequest("old", "new", "new")
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected session expired message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun sendPasswordChangeRequest_400Match_postsPasswordsDoNotMatch() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setResponseCode(400)
                .setBody("New passwords do not match")
        )
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Passwords do not match") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.sendPasswordChangeRequest("old", "new1", "new2")
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected password mismatch message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun sendPasswordChangeRequest_500_postsServerError() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(MockResponse().setResponseCode(500).setBody("Internal server error"))
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Server error") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.sendPasswordChangeRequest("old", "new", "new")
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected server error message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    @Test
    fun sendPasswordChangeRequest_networkFailure_postsNetworkFailure() {
        RuntimeEnvironment.getApplication()
        val server = MockWebServer()
        server.enqueue(MockResponse().setSocketPolicy(SocketPolicy.DISCONNECT_AT_START))
        server.start()

        val viewModel = UserViewModel()
        setPrivateUrl(viewModel, "userBaseUrl", server.url("/api/User").toString())

        val latch = CountDownLatch(1)
        val observer = Observer<String?> { msg ->
            if (msg == "Network failure") {
                latch.countDown()
            }
        }
        viewModel.errorMessage.observeForever(observer)

        try {
            viewModel.sendPasswordChangeRequest("old", "new", "new")
            val completed = latch.await(2, TimeUnit.SECONDS)
            assertTrue("Expected network failure message", completed)
        } finally {
            viewModel.errorMessage.removeObserver(observer)
            server.shutdown()
        }
    }

    private fun setPrivateUrl(viewModel: UserViewModel, fieldName: String, value: String) {
        val field = viewModel.javaClass.getDeclaredField(fieldName)
        field.isAccessible = true
        field.set(viewModel, value.removeSuffix("/"))
    }
}
