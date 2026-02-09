package com.pocketree.app

import android.content.Context
import androidx.lifecycle.MutableLiveData
import androidx.lifecycle.ViewModel
import com.google.gson.reflect.TypeToken
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException
import kotlin.jvm.java

// creation of a SharedViewModel to enable passing of data between fragments

data class UserState(
    val username: String = "User",
    val totalCoins: Int = 0,
    val currentLevelID: Int = 1,
    val levelName: String = "Seedling",
    val levelImageUrl: String = "",
    val profileImageUrl: String = "",
    val isWithered: Boolean = false,
    val plantHealthPercent: Int = 100
)

class UserViewModel: ViewModel() {

    val userState = MutableLiveData<UserState>()
    val isMusicPlaying = MutableLiveData<Boolean>()
//    val playSoundEffectEvent = MutableLiveData<Boolean>()
    val skins = MutableLiveData<List<Skin>>()  // by Chenyu
    val vouchers = MutableLiveData<List<Voucher>>()  // by Chenyu

    // UI state livedata
    val tasks = MutableLiveData<List<Task>?>()
    val latestBadgeName = MutableLiveData<String?>()
    val earnedBadges = MutableLiveData<List<Badge>>()
    val statusMessage = MutableLiveData<String?>() // for status on image verification
    val adminMessage = MutableLiveData<String?>()

    // event livedata
    val levelUpEvent = MutableLiveData<Triple<String,String,String>>()
    val isAiVerifying = MutableLiveData<Boolean>(false) // for loading progress bar (for ML image verification)
    val errorMessage = MutableLiveData<String?>()
    val redeemSkinSuccessEvent = MutableLiveData<String?>()  // by Chenyu
    val equipSkinSuccessEvent = MutableLiveData<String?>() // by Chenyu
    val redeemVoucherSuccessEvent = MutableLiveData<String?>() // by Chenyu
    val logoutSuccess = MutableLiveData<Boolean>()
    val passwordUpdateSuccess = MutableLiveData<Boolean>()

    private val client = NetworkClient.okHttpClient
    private val gson = NetworkClient.gson

    private val taskBaseUrl = ApiConfiguration.TASK_API_URL
    private val userBaseUrl = ApiConfiguration.USER_API_URL

//
//    // ==========================================
//    // MOCK data testing, to trigger withering
//    // ==========================================
//    fun mockWitheredState() {
//        val current = userState.value ?: UserState()
//        userState.postValue(current.copy(
//            isWithered = true,
//            plantHealthPercent = 0,
//            levelName = "Withered State (Mock)"
//        ))
//    }
//
//    // MOCK data testing, to trigger revive
//    fun mockReviveState() {
//        val current = userState.value ?: UserState()
//        userState.postValue(current.copy(
//            isWithered = false,
//            plantHealthPercent = 100,
//            levelName = "Revived State (Mock)"
//        ))
//    }

    // helper function - to update all LiveData at once
    private fun updateLiveData (user:User) {
        if (user == null) return // don't proceed if entire user object is null

        val newState = UserState(
            username = user.username ?: "User",
            totalCoins = user.totalCoins ?: 0,
            currentLevelID = user.currentLevelId ?: 1,
            levelName = user.levelName ?: "Seedling",
            levelImageUrl = user.levelImageUrl ?: "",
            profileImageUrl = user.profileImageUrl ?: "",
            isWithered = user.isWithered ?: false,
            plantHealthPercent = user.plantHealthPercent ?: 100
        )
        userState.postValue(newState)

        // save to cache so it's there during next app restart
        NetworkClient.saveUserCache(MyApplication.getContext(), user)
    }

    // used for passing data when moving from Login to Main activity
    // for use by fragments
    fun updateUserData(
        username: String,
        totalCoins: Int,
        currentLevelId: Int,
        levelName: String,
        isWithered: Boolean,
        levelImageUrl: String?,
        profileImageUrl: String?,
        plantHealthPercent: Int
    ) {
        val newState = UserState(
            username,
            totalCoins,
            currentLevelId,
            levelName,
            levelImageUrl ?: "",
            profileImageUrl ?:"",
            isWithered,
            plantHealthPercent
        )
        userState.value = newState // use .value for main thread calls

        // now that the main profile is set, go get the rest of the required info
        if (tasks.value.isNullOrEmpty()){
            fetchDailyTasks()
            fetchLatestBadges()
        }
    }
//
//    // for sound effects
//    fun triggerSoundEffect() {
//        playSoundEffectEvent.value = true
//    }

    // needed for updating whole UI (when task is completed, item redeemed etc)
    fun fetchUserProfile() {
        val request = Request.Builder()
            .url("${userBaseUrl}/GetUserProfileApi")
            .get()
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onResponse(call: Call, response: Response) {
                val responseBody = response.body?.string()

                if (response.isSuccessful && !responseBody.isNullOrEmpty()) {
                    try{
                        val user = gson.fromJson(responseBody, User::class.java)

                        if (user!= null) {
                            updateLiveData(user)

                            fetchLatestBadges()
                        } else {
                            errorMessage.postValue("Invalid user data")
                        }
                    } catch (e:Exception) {
                        errorMessage.postValue("Parsing error: ${e.message}")
                    }
                } else {
                    errorMessage.postValue("Failed to load profile.")
                }
            }
            override fun onFailure(call: Call, e: okio.IOException) {
                e.printStackTrace()
                errorMessage.postValue("Network error loading profile : ${e.message}")
            }
        })
    }

    fun loadCachedData(context: Context) {

        NetworkClient.loadUserCache(context)?.let { cachedUser ->
            updateLiveData(cachedUser)
        }

        // ensure LiveData is never null - initialise with empty lists if needed
        if (tasks.value == null) {
            tasks.value = emptyList()
        }
        if (earnedBadges.value == null) {
            earnedBadges.value = emptyList()
        }

        // fetch fresh data from server in background
        fetchDailyTasks()
        fetchLatestBadges()
    }

    fun fetchDailyTasks(){
        val request = Request.Builder()
            .url("${taskBaseUrl}/GetDailyTasksApi")
            .get()
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onResponse(call: Call, response: Response) {
                val responseBody = response.body?.string() ?: ""

                if (response.isSuccessful && responseBody.isNotEmpty()) {
                    try {
                        val taskListType = object : TypeToken<List<Task>>() {}.type
                        val fetchedTasks: List<Task> = gson.fromJson(responseBody, taskListType)
                        tasks.postValue(fetchedTasks)
                    } catch (e: Exception) {
                        errorMessage.postValue("Parsing error")
                        // keep existing value or set to empty if null
                        if (tasks.value == null) {
                            tasks.postValue(emptyList())
                        }
                    }
                } else {
                    errorMessage.postValue("Failed to load tasks.")
                    // keep existing value or set to empty if null
                    if (tasks.value == null) {
                        tasks.postValue(emptyList())
                    }
                }
            }
            override fun onFailure(call: Call, e: IOException) {
                e.printStackTrace()
                errorMessage.postValue("Network error loading tasks")
                // ensure tasks is never null
                if (tasks.value == null) {
                    tasks.postValue(emptyList())
                }
            }
        })
    }

    fun submitTask(taskId: Int, status: String, imageBytes: ByteArray? = null) {

        val requestBodyBuilder = MultipartBody.Builder()
            .setType(MultipartBody.FORM)
            .addFormDataPart("taskId", taskId.toString())
            .addFormDataPart("status", status)

        // Only add photo if it exists
        imageBytes?.let {
            requestBodyBuilder.addFormDataPart("photo", "upload.jpg",
                it.toRequestBody("image/jpeg".toMediaTypeOrNull()))
        }

        val request = Request.Builder()
            .url("${taskBaseUrl}/RecordTaskCompletionApi")
            .post(requestBodyBuilder.build())
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onResponse(call: Call, response: Response) {
                val bodyString = response.body?.string()

                if (response.isSuccessful && !bodyString.isNullOrEmpty()) {
                    try {
                        val result = gson.fromJson(bodyString, TaskCompletionResponse::class.java)

                        // update state using copy - bc data classes are immutable! so property cannot be changed
                        if (result.success) {
                            val current = userState.value ?: UserState()

                            // update task list
                            val currentTasks = tasks.value?.toMutableList()

                            currentTasks?.find { it.taskID == taskId }?.apply {
                                isCompleted = (status == "Completed")
                                isPassed = (status == "Passed")
                            }

                            // find the tasks we just finished and update it in memory
                            tasks.postValue(currentTasks) // update UI so it shows "Completed"

                            userState.postValue(
                                current.copy(
                                    totalCoins = result.newCoins,
                                    currentLevelID = result.newLevel,
                                    levelName = result.newLevelName, //?.takeIf { it.isNotEmpty() } ?: current.levelName, // update level name
                                    isWithered = result.isWithered,
                                    plantHealthPercent = result.plantHealthPercent
                                )
                            )

                            if (result.levelUp) {
                                // use simple mapping for vouchers
                                val voucherName = when (result.newLevel) {
                                    2 -> "Voucher 1"
                                    3 -> "Voucher 2"
                                    else -> ""
                                }

                                // fetch badge name first, then post complete event with all details
                                fetchLatestBadgeForLevelUp(result.newLevelName ?: "new", voucherName)
                            }
                        } else {
                            errorMessage.postValue("Task submission failed")
                        }
                    } catch (e:Exception) {
                        errorMessage.postValue("Error processing response: ${e.message}")
                    }
                } else {
                    errorMessage.postValue("Server error")
                }
            }
            override fun onFailure(call: Call, e: IOException) {
                errorMessage.postValue("Network error.")
            }
        })
    }

    //ff (partially edited by shirley)
    fun processTaskWithVerification(id: Int, keyword: String, imageBytes: ByteArray) {
        isAiVerifying.postValue(true)
        statusMessage.postValue("AI is verifying your photo...")

        // Prepare the Multipart data for Retrofit
        val requestFile = imageBytes.toRequestBody("image/jpeg".toMediaTypeOrNull())
        val body = MultipartBody.Part.createFormData("file", "room_with_tv.jpg", requestFile)
        val keywordBody = keyword.toRequestBody("text/plain".toMediaTypeOrNull())

        RetrofitClient.mlInstance.verifyImage(body, keywordBody).enqueue(object : retrofit2.Callback<VerificationResponse> {
            override fun onResponse(call: retrofit2.Call<VerificationResponse>, response: retrofit2.Response<VerificationResponse>) {
                if (response.isSuccessful && response.body()?.isVerified == true) {
                    // SUCCESS: Proceed to submit to your C# backend
                    isAiVerifying.postValue(false)
                    statusMessage.postValue("Verification success")
                    submitTask(id, "Completed", imageBytes)
                } else {
                    // FAILURE
                    isAiVerifying.postValue(false)
                    statusMessage.postValue(null)
                    errorMessage.postValue("$keyword could not be found.\nPlease try again!")
                    submitTask(id, "Failed", imageBytes)
                }
            }

            override fun onFailure(call: retrofit2.Call<VerificationResponse>, t: Throwable) {
                // NETWORK FAILURE
                isAiVerifying.postValue(false)
                statusMessage.postValue(null)
                errorMessage.postValue("Verification error: ${t.message}")
            }
        })
    }

    fun fetchLatestBadges(shouldNotifyBadge:Boolean = false) {
        val request = Request.Builder()
            .url("$userBaseUrl/GetLatestBadgesApi")
            .get()
            .build()

        client.newCall(request).enqueue(object:Callback {
            override fun onResponse(call:Call, response:Response) {
                if (response.isSuccessful) {
                    try {
                        val json = response.body?.string()
                        val badgeListType = object: TypeToken<List<Badge>>() {}.type
                        val allFetchedBadges: List<Badge> = gson.fromJson(json, badgeListType)

                        // post list of badges for UI to display
                        val displayBadges = allFetchedBadges.take(3)
                        earnedBadges.postValue(displayBadges)

                        // extract name of latest badge for level up dialog
                        if (shouldNotifyBadge && allFetchedBadges.isNotEmpty()) {
                            val latestName = allFetchedBadges[0].badgeName
                            latestBadgeName.postValue(latestName)
                        }
                    } catch (e:Exception) {
                        if (earnedBadges.value == null) {
                            earnedBadges.postValue(emptyList())
                        }
                    }
                } else {
                    if (earnedBadges.value == null) {
                        earnedBadges.postValue(emptyList())
                    }
                }
            }
            override fun onFailure(call:Call, e:IOException) {
                e.printStackTrace()
                if (earnedBadges.value == null) {
                    earnedBadges.postValue(emptyList())
                }
            }
        })
    }

    // method for level up - to fetch badge name and trigger complete event
    fun fetchLatestBadgeForLevelUp(levelName: String, voucherName: String) {
        val request = Request.Builder()
            .url("$userBaseUrl/GetLatestBadgesApi")
            .get()
            .build()

        client.newCall(request).enqueue(object:Callback {
            override fun onResponse(call:Call, response:Response) {
                if (response.isSuccessful) {
                    try {
                        val json = response.body?.string()
                        val badgeListType = object: TypeToken<List<Badge>>() {}.type
                        val allFetchedBadges: List<Badge> = gson.fromJson(json, badgeListType)

                        // post list of badges for UI to display
                        val displayBadges = allFetchedBadges.take(3)
                        earnedBadges.postValue(displayBadges)

                        // extract name of latest badge
                        val badgeName = if (allFetchedBadges.isNotEmpty()) {
                            allFetchedBadges[0].badgeName
                        } else {
                            ""
                        }

                        // Now post the complete event with level, badge, and voucher
                        levelUpEvent.postValue(Triple(levelName, badgeName ?: "", voucherName))
                    } catch (e:Exception) {
                        // Still post event even if badge fetch fails
                        levelUpEvent.postValue(Triple(levelName, "", voucherName))
                        if (earnedBadges.value == null) {
                            earnedBadges.postValue(emptyList())
                        }
                    }
                } else {
                    // Still post event even if request fails
                    levelUpEvent.postValue(Triple(levelName, "", voucherName))
                    if (earnedBadges.value == null) {
                        earnedBadges.postValue(emptyList())
                    }
                }
            }
            override fun onFailure(call:Call, e:IOException) {
                e.printStackTrace()
                // Still post event even if network fails
                levelUpEvent.postValue(Triple(levelName, "", voucherName))
                if (earnedBadges.value == null) {
                    earnedBadges.postValue(emptyList())
                }
            }
        })
    }

    fun fetchSkins() {
        val request = Request.Builder()
            .url("$userBaseUrl/GetSkinsShopApi")
            .get()
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onResponse(call: Call, response: Response) {
                val body = response.body?.string()
                if (response.isSuccessful && !body.isNullOrEmpty()) {
                    try {
                        val listType = object : TypeToken<List<Skin>>() {}.type
                        val fetchedList: List<Skin> = gson.fromJson(body, listType)
                        skins.postValue(fetchedList)
                    } catch (e: Exception) {
                        e.printStackTrace()
                        errorMessage.postValue("Error parsing skins")
                    }
                } else {
                    errorMessage.postValue("Failed to load skins")
                }
            }

            override fun onFailure(call: Call, e: IOException) {
                errorMessage.postValue("Network error fetching skins")
            }
        })
    }

    fun redeemSkin(skinId:Int) {
        val body = skinId.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        val request = Request.Builder()
            .url("$taskBaseUrl/RedeemSkinApi")
            .post(body)
            .build()

        client.newCall(request).enqueue(object:Callback {
            override fun onResponse(call:Call, response:Response) {
                if (response.isSuccessful) {
                    val json = response.body?.string()
                    val result = gson.fromJson(json, Map::class.java)
                    val newBalance = (result["newCoins"] as Double).toInt()
                    // Gson defaults to treating all numbers as Doubles
                    // when parsing into a generic Map<String, Any>
                    val currentState = userState.value ?: UserState()
                    userState.postValue(currentState.copy(totalCoins=newBalance))
                    redeemSkinSuccessEvent.postValue("Skin redeemed successfully!")
                } else {
                    val errorBody = response.body?.string() ?: "Unknown error"
                    errorMessage.postValue("Failed: $errorBody")
                }
            }
            override fun onFailure(call:Call, e:IOException) {
                errorMessage.postValue("Network error.")
            }
        })
    }

    fun equipSkin(skinId: Int) {
        val body = skinId.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        val request = Request.Builder()
            .url("$taskBaseUrl/EquipSkinApi")
            .post(body)
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onResponse(call: Call, response: Response) {
                if (response.isSuccessful) {
                    equipSkinSuccessEvent.postValue("Skin equipped successfully!")
                } else {
                    val errorBody = response.body?.string() ?: "Unknown error"
                    errorMessage.postValue("Failed: $errorBody")
                }
            }

            override fun onFailure(call: Call, e: IOException) {
                errorMessage.postValue("Network error.")
            }
        })
    }

    fun fetchVouchers() {
        val request = Request.Builder()
            .url("$userBaseUrl/GetAllVouchersApi")
            .get()
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onResponse(call: Call, response: Response) {
                val body = response.body?.string()
                if (response.isSuccessful && !body.isNullOrEmpty()) {
                    try {
                        val listType = object : TypeToken<List<Voucher>>() {}.type
                        val fetchedList: List<Voucher> = gson.fromJson(body, listType)
                        vouchers.postValue(fetchedList)
                    } catch (e: Exception) {
                        errorMessage.postValue("Error parsing vouchers")
                    }
                }
            }
            override fun onFailure(call: Call, e: IOException) {
                errorMessage.postValue("Network error fetching vouchers")
            }
        })
    }

    fun redeemVoucher(voucherId: Int) {
        val body = voucherId.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        val request = Request.Builder()
            .url("$taskBaseUrl/RedeemVoucherApi")
            .post(body)
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onResponse(call: Call, response: Response) {
                if (response.isSuccessful) {
                    redeemVoucherSuccessEvent.postValue("Voucher used successfully!")
                } else {
                    val errorBody = response.body?.string() ?: "Unknown error"
                    errorMessage.postValue("Failed: $errorBody")
                }
            }

            override fun onFailure(call: Call, e: IOException) {
                e.printStackTrace()
                errorMessage.postValue("Network error.")
            }
        })
    }

    fun postAdminMessage(message:String) {
        adminMessage.postValue(message)
    }

    fun logout() {
        val context = MyApplication.getContext()

        val request = Request.Builder()
            .url("$userBaseUrl/LogoutApi")
            .post("".toRequestBody(null))
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onFailure(call: Call, e: IOException) {
                // Automatically switches to Main Thread to update observers
                errorMessage.postValue("Network failure")
            }

            override fun onResponse(call: Call, response: Response) {
                response.use {
                    if (response.isSuccessful) {
                        performLocalCleanup(context)

                        // Notify the UI that logout was successful
                        logoutSuccess.postValue(true)
                    } else {
                        errorMessage.postValue("Logout failed")
                    }
                }
            }
        })
    }

    fun sendPasswordChangeRequest(current:String, new:String, confirm:String) {
        val jsonObject = JSONObject().apply{
            put("CurrentPassword", current)
            put("NewPassword", new)
            put("ConfirmNewPassword", confirm)
        }

        val body = jsonObject.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        val request = Request.Builder()
            .url("$userBaseUrl/change-password")
            .post(body)
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onFailure(call: Call, e: IOException) {
                errorMessage.postValue("Network failure")
            }

            override fun onResponse(call: Call, response: Response) {
                val responseBody = response.body?.string() ?: ""
                if (response.isSuccessful) {
                    passwordUpdateSuccess.postValue(true)
                } else {
                    val msg = when (response.code) {
                        // 400 Bad Request (Business Logic Error)
                        400 -> {
                            if (responseBody.contains("incorrect", ignoreCase = true)){
                                "Invalid password"
                            } else if (responseBody.contains("match", ignoreCase = true)) {
                                "Passwords do not match"
                            } else { // other validation errors
                                "Validation failed"
                            }
                        }
                        401 -> "Session expired"
                        500 -> "Server error"
                        else -> "Error"
                    }
                    errorMessage.postValue(msg)
                }
            }
        })
    }

    fun performLocalCleanup(context:Context) { // "logging out" locally on android device
        // clearing token in Network Client
        NetworkClient.setToken(context, null)

        // clear user cache
        val prefs = context.getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        prefs.edit().clear().apply()

        // reset ViewModel state
        userState.postValue(UserState())
        tasks.postValue(emptyList())
        earnedBadges.postValue(emptyList())
    }
}
