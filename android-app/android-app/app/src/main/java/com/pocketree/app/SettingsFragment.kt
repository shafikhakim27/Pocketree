package com.pocketree.app

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Toast
import androidx.fragment.app.Fragment
import com.pocketree.app.databinding.FragmentSettingsBinding

class SettingsFragment: Fragment() {
    private var _binding: FragmentSettingsBinding? = null
    private val binding get() = _binding!!

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        _binding = FragmentSettingsBinding.inflate(inflater, container, false)
        return binding.root
    }


    // kiv logout logic
//    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
//        super.onViewCreated(view, savedInstanceState)
//
//        // ... your other setup ...
//
//        binding.settingsTab.setOnClickListener {
//            // 1. Clear the data in ViewModel
//            userViewModel.logout()
//
//            // 2. Navigate back to Login (Assuming you are using Fragments)
//            parentFragmentManager.beginTransaction()
//                .replace(R.id.fragment_container, LoginFragment())
//                .commit()
//
//            // 3. Optional: Show a toast
//            Toast.makeText(context, "Logged out successfully", Toast.LENGTH_SHORT).show()
//        }
//    }

    override fun onDestroyView(){
        super.onDestroyView()
        _binding = null
    }
}