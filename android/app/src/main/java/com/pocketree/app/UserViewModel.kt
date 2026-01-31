package com.pocketree.app

import android.content.Context
import android.util.Log
import android.util.Log.e
import android.widget.Toast
import androidx.lifecycle.MutableLiveData
import androidx.lifecycle.ViewModel
import com.google.gson.reflect.TypeToken
import com.pocketree.app.NetworkClient.gson
import com.pocketree.app.UserState
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException
import java.util.concurrent.ThreadLocalRandom.current
import kotlin.jvm.java

// creation of a SharedViewModel to enable passing of data between fragments


data class UserState(
    val username: String = "User",
    val totalCoins: Int = 0,
    val currentLevelID: Int = 1,
    val levelName: String = "Seedling",
    val levelImageUrl: String = "",
    val isWithered: Boolean = false,
)

class UserViewModel: ViewModel() {

    val userState = MutableLiveData<UserState>()

    // UI state livedata
    val tasks = MutableLiveData<List<Task>>()
    val earnedBadges = MutableLiveData<List<Badge>>()
    val redeemSuccessEvent = MutableLiveData<String?>()

    // event livedata
    val levelUpEvent = MutableLiveData<Boolean>()
    val isLoading = MutableLiveData<Boolean>(false) // for loading of progress bar (for ML image verification)
    val errorMessage = MutableLiveData<String>()

    private val client = NetworkClient.okHttpClient
    private val gson = NetworkClient.gson

    private val taskBaseUrl = "http://10.0.2.2:5042/api/Task"
    private val userBaseUrl = "http://10.0.2.2:5042/api/User"

    // helper function - to update all LiveData at once
    private fun updateLiveData (user:User) {
        if (user == null) return // don't proceed if entire user object is null

        val newState = UserState(
            username = user.username ?: "User",
            totalCoins = user.totalCoins ?: 0,
            currentLevelID = user.currentLevelId ?: 1,
            levelName = user.levelName ?: "Seedling",
            levelImageUrl = user.levelImageUrl ?: "",
            isWithered = user.isWithered ?: false
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
        levelImageUrl: String?
    ) {
        val newState = UserState(
            username,
            totalCoins,
            currentLevelId,
            levelName,
            levelImageUrl ?: "",
            isWithered
        )
        userState.value = newState // use .value for main thread calls

        // Now that the main profile is set, go get the rest
        if (tasks.value.isNullOrEmpty()){
            fetchDailyTasks()
            fetchEarnedBadges()
        }
    }

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
                        updateLiveData(user)

                        // fetch tasks and badges after user profile is loaded
                        fetchDailyTasks()
                        fetchEarnedBadges()
                    } catch (e:Exception) {
                        errorMessage.postValue("Parsing error")
                    }
                } else {
                    errorMessage.postValue("Failed to load profile.")
                }
            }
            override fun onFailure(call: Call, e: okio.IOException) {
                e.printStackTrace()
            }
        })
    }

    fun loadCachedData(context: Context) {
        NetworkClient.loadUserCache(context)?.let{ cachedUser ->
            updateLiveData(cachedUser)
        }
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
                    }
                } else {
                    errorMessage.postValue("Failed to load tasks.")
                }
            }
            override fun onFailure(call: Call, e: IOException) { e.printStackTrace() }
        })
    }

    fun submitTask(taskId: Int, status: String, imageBytes: ByteArray? = null) {
        isLoading.postValue(true)

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
                isLoading.postValue(false)
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
                                    isWithered = result.isWithered
                                )
                            )

                            if (result.levelUp) levelUpEvent.postValue(true)
                            fetchEarnedBadges()
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
                isLoading.postValue(false)
                errorMessage.postValue("Network error.")
            }
        })
    }

    // needed for when levelling up - which badge and voucher is tied to which level
    fun getLevelDetails(): Triple<String, String, String>{
        val currLevelID = userState.value?.currentLevelID ?: 1
        return when (currLevelID) {
            2 -> Triple ("Sapling", "Sapling Badge", "Voucher 1")
            3 -> Triple ("Mighty Oak", "Oak Badge", "Voucher 2")
            else -> Triple("Seedling", "No Badge", "No Voucher")
        }
    }

    // used for redemption of skins
    fun updateTotalCoins(newTotal:Int) {
        val currentState = userState.value ?: UserState()
        userState.postValue(currentState.copy(totalCoins=newTotal))
    }

    fun fetchEarnedBadges() {
        val request = Request.Builder()
            .url("$userBaseUrl/GetEarnedBadgesApi")
            .get()
            .build()

        client.newCall(request).enqueue(object:Callback {
            override fun onResponse(call:Call, response:Response) {
                if (response.isSuccessful) {
                    val json = response.body?.string()
                    val badgeListType = object: TypeToken<List<Badge>>() {}.type
                    val allFetchedBadges: List<Badge> = gson.fromJson(json, badgeListType)
                    val displayBadges = allFetchedBadges.take(3)

                    earnedBadges.postValue(displayBadges)
                }
            }
            override fun onFailure(call:Call, e:IOException) {e.printStackTrace()}
        })
    }

    // check back again
    fun redeemSkin(skinId:Int) {
        val json = JSONObject().apply {
            put("SkinID", skinId)
        }

        val body = json.toString().toRequestBody("application/json; charset=utf-8".toMediaType())


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

                    redeemSuccessEvent.postValue("Skin redeemed successfully!")

                    //fetchUserProfile()
                } else {
                    errorMessage.postValue("Insufficient coins or server error.")
                }
            }
            override fun onFailure(call:Call, e:IOException) {
                errorMessage.postValue("Network error.")
            }
        })
    }

    fun logout() {
        // clearing token in Network Client
        NetworkClient.setToken(MyApplication.getContext(), null)

        // reset state to default
        userState.value = UserState()

        // clear lists
        tasks.value = emptyList()
        earnedBadges.value = emptyList()
    }
}

// to implement logic of viewing all badges with "See More" later on