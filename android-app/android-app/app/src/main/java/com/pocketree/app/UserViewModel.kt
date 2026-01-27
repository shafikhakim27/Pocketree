package com.pocketree.app

import android.util.Log
import android.widget.Toast
import androidx.lifecycle.MutableLiveData
import androidx.lifecycle.ViewModel
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import com.pocketree.app.NetworkClient.gson
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException

// creation of a SharedViewModel to enable passing of data between fragments

class UserViewModel: ViewModel() {

    // LiveData is used so that UI updates automatically if coins change
    val username = MutableLiveData<String>()
    val totalCoins = MutableLiveData<Int>()
    val levelName = MutableLiveData<String>()
    val tasks = MutableLiveData<List<Task>>()
    val isLoading = MutableLiveData<Boolean>(false) // for loading of progress bar (for ML image verification)
    val levelImageUrl = MutableLiveData<String>()
    val isWithered = MutableLiveData<Boolean>()
    val levelUpEvent = MutableLiveData<Boolean>()
    val errorMessage = MutableLiveData<String>()

    private val client = NetworkClient.okHttpClient
    private val gson = NetworkClient.gson
    private val baseUrl = "http://10.0.2.2:5000/api/Task"

    fun loginUser(credentials: JSONObject) {
        val body = credentials.toString()
            .toRequestBody("application/json; charset=utf-8".toMediaType())
        val request = Request.Builder()
            .url("$baseUrl/LoginApi")
            .post(body)
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onResponse(call: Call, response: Response) {
                if (response.isSuccessful) {
                    val jsonResponse = response.body?.string()
                    val data = gson.fromJson(jsonResponse, LoginResponse::class.java)

                    // save the token to the Singleton so the Interceptor can find it
                    NetworkClient.setToken(NetworkClient.context, data.token)

                    // update UI livedata
                    username.postValue(data.user.username)
                    totalCoins.postValue(data.user.totalCoins)

                    // fetch tasks now that user is logged in
                    fetchDailyTasks()
                }
            }

            override fun onFailure(call: Call, e: okio.IOException) {
                e.printStackTrace()
            }
        })
    }

    fun fetchUserProfile() {
//        isLoading.postValue(true) // start loading

        val request = Request.Builder()
            .url("${baseUrl}/GetUserProfileApi")
            .get()
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onResponse(call: Call, response: Response) {
//                isLoading.postValue(false)
                if (response.isSuccessful) {
                    val json = response.body?.string()
                    val user = gson.fromJson(json, User::class.java)

                    // update all LiveData at once
                    username.postValue(user.username)
                    totalCoins.postValue(user.totalCoins)
                    levelName.postValue(user.levelName)
                    levelImageUrl.postValue(user.levelImageUrl)
                    isWithered.postValue(user.isWithered)
                    tasks.postValue(user.tasks)
                }
            }

            override fun onFailure(call: Call, e: okio.IOException) {
//                isLoading.postValue(false)
                e.printStackTrace()
            }
        })
    }

    fun fetchDailyTasks(){
        val request = Request.Builder()
            .url("${baseUrl}/GetDailyTasksApi")
            .build()

        client.newCall(request).enqueue(object : Callback {
            override fun onResponse(call: Call, response: Response) {
                if (response.isSuccessful) {
                    val json = response.body?.string()
                    val taskListType = object: TypeToken<List<Task>>() {}.type
                    // TypeToken helps to retain generic type information
                    val fetchedTasks: List<Task> = gson.fromJson(json, taskListType)
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
            .addFormDataPart("taskId", taskId.toString())
            // represents one section within multipart body (ie a file or a form field)
            .addFormDataPart("photo", "upload.jpg",
                imageBytes.toRequestBody("image/jpeg".toMediaTypeOrNull()))
            .build()

        val request = Request.Builder()
            .url("${baseUrl}/SubmitTaskApi")
            .post(requestBody)
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onResponse(call: Call, response: Response) {
                if (response.isSuccessful) {
                    val jsonResponse = response.body?.string()
                    val result = gson.fromJson(jsonResponse, Map::class.java)
                    // we tell Gson to treat the JSOn as a generic Map

                    // check if verification was successful
                    if (result["success"] == "true") {
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
            put("TaskId", taskId)
        }
        val body = json.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        val request = Request.Builder()
            .url("$baseUrl/RecordTaskCompletionApi")
            .post(body)
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onResponse(call: Call, response: Response) {
                isLoading.postValue(false)
                if (response.isSuccessful) {
                    val jsonResponse = response.body?.string()
                    val result = gson.fromJson(jsonResponse, Map::class.java)

                    // check for level up
                    val isLevelUp = result["levelUp"] as? Boolean ?: false
                    if (isLevelUp) {
                        levelUpEvent.postValue(true)
                    }

                    fetchUserProfile() // refresh profile to get new total coins and level name
                    fetchDailyTasks() // refresh list so the task shows as completed
                }
            }
            override fun onFailure(call: Call, e: IOException) { e.printStackTrace() }
        })
    }

    fun updateTotalCoins(newTotal:Int) {
        totalCoins.postValue(newTotal)
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