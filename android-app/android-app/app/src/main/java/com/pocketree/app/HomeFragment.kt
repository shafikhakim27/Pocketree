package com.pocketree.app

import android.os.Bundle
import android.view.View
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import com.pocketree.app.databinding.FragmentHomeBinding

class HomeFragment: Fragment() {
    private var _binding: FragmentHomeBinding? = null
    private val binding get() = _binding!!

    // ViewModel (to get the data)
    private val sharedViewModel: UserViewModel by activityViewModels()

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        _binding = FragmentHomeBinding.bind(view)

        sharedViewModel.username.observe(viewLifecycleOwner) { name ->
            binding.accountInfo.text = "Hello, $name"
        }

        sharedViewModel.totalCoins.observe(viewLifecycleOwner) { coins ->
            binding.coinDisplay.text = "$coins pts"
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
