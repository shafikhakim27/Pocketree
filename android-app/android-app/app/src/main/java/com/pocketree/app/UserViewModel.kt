package com.pocketree.app

import androidx.lifecycle.MutableLiveData
import androidx.lifecycle.ViewModel
import com.google.gson.reflect.TypeToken
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException

// creation of a SharedViewModel to enable passing of data between fragments

class UserViewModel: ViewModel() {
    // LiveData is used so that UI updates automatically if coins change

    // UI state livedata
    val username = MutableLiveData<String>()
    val totalCoins = MutableLiveData<Int>()
    val currentLevelID = MutableLiveData<Int>()
    val levelName = MutableLiveData<String>()
    val levelImageUrl = MutableLiveData<String>()
    val isWithered = MutableLiveData<Boolean>()
    val tasks = MutableLiveData<List<Task>>()
    val recentBadges = MutableLiveData<List<Badge>>()
    val earnedBadges = MutableLiveData<List<Badge>>()

    // event livedata
    val levelUpEvent = MutableLiveData<Boolean>()
    val isLoading = MutableLiveData<Boolean>(false) // for loading of progress bar (for ML image verification)
    val errorMessage = MutableLiveData<String>()

    private val client = NetworkClient.okHttpClient
    private val gson = NetworkClient.gson

    private val taskBaseUrl = "http://10.0.2.2:5042/api/Task"
    private val userBaseUrl = "http://10.0.2.2:5042/api/User"

    // needed for updating whole UI (when task is completed, item redeemed etc)
    fun fetchUserProfile() {

        val request = Request.Builder()
            .url("${userBaseUrl}/GetUserProfileApi")
            .get()
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onResponse(call: Call, response: Response) {
                if (response.isSuccessful) {
                    val json = response.body?.string()
                    val user = gson.fromJson(json, User::class.java)

                    updateLiveData(user)

                    // fetch tasks and badges after user profile is loaded
                    fetchDailyTasks()
                    fetchEarnedBadges()
                } else {
                    errorMessage.postValue("Failed to load profile.")
                }
            }

            override fun onFailure(call: Call, e: okio.IOException) {
                e.printStackTrace()
            }
        })
    }

    // helper function
    private fun updateLiveData (user:User) {
        // update all LiveData at once
        username.postValue(user.username)
        totalCoins.postValue(user.totalCoins)
        currentLevelID.postValue(user.currentLevelId)
        levelName.postValue(user.levelName)
        levelImageUrl.postValue(user.levelImageUrl)
        isWithered.postValue(user.isWithered)
    }

    // used for passing data when moving from Login to Main activity
    fun updateUserData(
        username: String,
        totalCoins: Int,
        currentLevelId: Int,
        levelName: String,
        isWithered: Boolean,
        levelImageUrl: String?
    ) {
        // Push the values to LiveData
        this.username.value = username
        this.totalCoins.value = totalCoins
        this.currentLevelID.value = currentLevelId
        this.levelName.value = levelName
        this.isWithered.value = isWithered
        this.levelImageUrl.value = levelImageUrl ?: ""

        // Now that the main profile is set, go get the rest
        fetchDailyTasks()
        fetchEarnedBadges()
    }

    fun fetchDailyTasks(){
        val emptyBody = "".toRequestBody("application/json".toMediaTypeOrNull())

        val request = Request.Builder()
            .url("${taskBaseUrl}/GetDailyTasksApi")
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onResponse(call: Call, response: Response) {
                val responseBody = response.body?.string() ?: ""

                if (response.isSuccessful) {
//                    val json = response.body?.string()
                    val taskListType = object : TypeToken<List<Task>>() {}.type
                    // TypeToken helps to retain generic type information
                    val fetchedTasks: List<Task> = gson.fromJson(responseBody, taskListType)
                    tasks.postValue(fetchedTasks) // update UI automatically
                }
            }
            override fun onFailure(call: Call, e: IOException) { e.printStackTrace() }
        })
    }

    fun submitTaskWithImage (taskId: Int, imageBytes: ByteArray) {
        isLoading.postValue(true) // start loading

        val requestBody = MultipartBody.Builder()
            .setType(MultipartBody.FORM)
            .addFormDataPart("TaskID", taskId.toString())
            // represents one section within multipart body (ie a file or a form field)
            .addFormDataPart("photo", "upload.jpg",
                imageBytes.toRequestBody("image/jpeg".toMediaTypeOrNull()))
            .build()

        val request = Request.Builder()
            .url("${taskBaseUrl}/SubmitTaskApi")
            .post(requestBody)
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onResponse(call: Call, response: Response) {
                if (response.isSuccessful) {
                    val jsonResponse = response.body?.string()
                    val result = gson.fromJson(jsonResponse, Map::class.java)
                    // we tell Gson to treat the JSOn as a generic Map

                    // check if verification was successful
                    if (result["success"] == true) {
                        completeTaskDirectly(taskId) // record task completion
                    } else {
                        isLoading.postValue(false)  // stop loading on verification failure
                        // show error message for unsuccessful verification
                        errorMessage.postValue("Image verification failed. Please try again.")
                    }
                } else {
                    isLoading.postValue(false) // stop loading on error
                    // handle verification failure
                    errorMessage.postValue("Verification failed. Please try again.")
                }
            }
            override fun onFailure(call: Call, e: IOException) {
                isLoading.postValue(false) // stop loading on error
                errorMessage.postValue("Network error. Please check your connection.")
                e.printStackTrace()
            }
        })
    }

    fun completeTaskDirectly(taskId: Int) {
        val json = JSONObject().apply{
            put("TaskID", taskId)
        }
        val body = json.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        val request = Request.Builder()
            .url("$taskBaseUrl/RecordTaskCompletionApi")
            .post(body)
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onResponse(call: Call, response: Response) {
                isLoading.postValue(false)
                if (response.isSuccessful) {
                    // instant revival on UI if plant is withered
                    isWithered.postValue(false)

                    // handle level up event
                    val jsonResponse = response.body?.string()
                    val result = gson.fromJson(jsonResponse, Map::class.java)

                    if (result["levelUp"]==true) {
                        levelUpEvent.postValue(true)
                    }

                    fetchUserProfile() // refresh profile to get new total coins and level name
                    fetchDailyTasks() // refresh list so the task shows as completed
                }
            }
            override fun onFailure(call: Call, e: IOException) { e.printStackTrace() }
        })
    }

    // needed for when levelling up - which badge and voucher is tied to which level
    fun getLevelDetails(): Triple<String, String, String>{
        return when (currentLevelID.value) {
            2 -> Triple ("Sapling", "Sapling Badge", "Voucher 1")
            3 -> Triple ("Mighty Oak", "Oak Badge", "Voucher 2")
            else -> Triple("Seedling", "No Badge", "No Voucher")
        }
    }

    // used for redemption of skins
    fun updateTotalCoins(newTotal:Int) {
        totalCoins.postValue(newTotal)
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

    fun logout() {
        // clearing token in Network Client
        NetworkClient.setToken(NetworkClient.context, null)

        // reset UI state immediately (using .value is faster, since logout is running on main thread)
        username.value=""
        totalCoins.value=0
        tasks.value=emptyList()
    }
}

// to implement logic of viewing all badges with "See More" later on