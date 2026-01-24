package com.pocketree.app

import androidx.lifecycle.MutableLiveData
import androidx.lifecycle.ViewModel
import com.google.gson.Gson
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
    val username = MutableLiveData<String>()
    val totalCoins = MutableLiveData<Int>()
    val currentLevelId = MutableLiveData<Int>()
    val tasks = MutableLiveData<List<Task>>()
    var userToken: String? = null //Json Web Token is usually received during login

    // use of interceptor to declare JWT token in every request
    private val client = OkHttpClient.Builder()
        .addInterceptor { chain ->
            val requestBuilder = chain.request().newBuilder()
            userToken?.let {
                requestBuilder.addHeader("Authorization", "Bearer $it")
            }
            chain.proceed(requestBuilder.build())
        }
        .build()
    private val gson = Gson()
    private val baseUrl = "http://10.0.2.2:5050/api/Task"

    fun fetchUserData(user: User) {
        username.value = user.username
        totalCoins.value = user.totalCoins
        currentLevelId.value = user.currentLevelId
        tasks.value = user.tasks
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

    fun submitTaskWithImage (userId: Int, taskId: Int, imageBytes: ByteArray) {
        val requestBody = MultipartBody.Builder()
            .setType(MultipartBody.FORM)
            .addFormDataPart("userId", userId.toString())
            // represents one section within multipart body (ie a file or a form field)
            .addFormDataPart("taskId", taskId.toString())
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
                    fetchDailyTasks()
                }
            }
            override fun onFailure(call: Call, e: IOException) { e.printStackTrace() }
        })
    }

    fun completeTaskDirectly(taskId: Int) {

        val json = JSONObject().apply{
            put("taskID", taskId)
        }
        val body = json.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        val request = Request.Builder()
            .url("$baseUrl/RecordTaskCompletionApi")
            .addHeader("Authorization", "Bearer $userToken") // security check
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
}