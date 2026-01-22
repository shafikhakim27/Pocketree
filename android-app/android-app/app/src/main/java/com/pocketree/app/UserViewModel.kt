package com.pocketree.app

import androidx.lifecycle.MutableLiveData
import androidx.lifecycle.ViewModel

// creation of a SharedViewModel to enable passing of data between fragments

class UserViewModel: ViewModel() {
    // LiveData is used so that UI updates automatically if coins change
    val username = MutableLiveData<String>()
    val totalCoins = MutableLiveData<Int>()
    val currentLevelId = MutableLiveData<Int>()
    val tasks = MutableLiveData<List<Task>>()

    fun setUserData(user: User) {
        username.value = user.username
        totalCoins.value = user.totalCoins
        currentLevelId.value = user.currentLevelId
        tasks.value = user.tasks
    }
}