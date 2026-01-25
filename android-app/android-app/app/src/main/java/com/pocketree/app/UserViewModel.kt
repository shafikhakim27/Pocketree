package com.pocketree.app

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
    val currentLevelId = MutableLiveData<Int>() // pending logic on level up
    val tasks = MutableLiveData<List<Task>>()
    val isLoading = MutableLiveData<Boolean>(false) // for loading of progress bar (for ML image verification)

    private val client = NetworkClient.okHttpClient
    private val gson = NetworkClient.gson
    private val baseUrl = "http://10.0.2.2:5050/api/Task"

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
                    currentLevelId.postValue(user.currentLevelId)
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
                isLoading.postValue(false) // stop loading
                if (response.isSuccessful) {
                    fetchDailyTasks()
                }
            }
            override fun onFailure(call: Call, e: IOException) {
                isLoading.postValue(false) // stop loading on error
                e.printStackTrace()
            }
        })
    }

    fun completeTaskDirectly(taskId: Int) {

        val json = JSONObject().apply{
            put("taskID", taskId)
        }
        val body = json.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        val request = Request.Builder()
            .url("$baseUrl/RecordTaskCompletionApi")
            .post(body)
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onResponse(call: Call, response: Response) {
                if (response.isSuccessful) {
                    fetchDailyTasks() // refresh list so the task shows as completed
                }
            }
            override fun onFailure(call: Call, e: IOException) { e.printStackTrace() }
        })
    }

    fun logout() {
        // clearing token in Network Client
        NetworkClient.setToken(NetworkClient.context, null)

        // clearing LiveData so UI resets
        tasks.postValue(emptyList())
        totalCoins.postValue(0)
        username.postValue("")
    }
}