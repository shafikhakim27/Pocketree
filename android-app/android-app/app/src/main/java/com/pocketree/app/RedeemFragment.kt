package com.pocketree.app

import android.app.AlertDialog
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Toast
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import androidx.recyclerview.widget.GridLayoutManager
import com.pocketree.app.databinding.FragmentRedeemBinding
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException

class RedeemFragment: Fragment() {
    private var _binding: FragmentRedeemBinding? = null
    private val binding get() = _binding!!

    private val sharedViewModel: UserViewModel by activityViewModels()
    private val client = NetworkClient.okHttpClient

    // mock data for now
    private val mockItems = listOf(
        Skin(1, "Item1", 10, R.drawable.redeem_item_1, true, true),
        Skin(2, "Item2", 20, R.drawable.redeem_item_2, true, false),
        Skin(3, "Item3", 30, R.drawable.redeem_item_3, false, false),
        Skin(4, "Item4", 40, R.drawable.redeem_item_4, false, false)
    )

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        _binding = FragmentRedeemBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        setupRecyclerView()

        sharedViewModel.username.observe(viewLifecycleOwner) { name ->
            binding.accountInfo.text = "${name ?: "User"}"
        }

        // observe coins to update coinDisplay TextView
        sharedViewModel.totalCoins.observe(viewLifecycleOwner) { coins ->
            binding.coinDisplay.text = "$coins coins"
        }
    }

    private fun setupRecyclerView() {
        // GridLayoutManager: parameter 3 indicates that 3 items are displayed in one row.
        binding.recyclerViewRedeem.layoutManager = GridLayoutManager(context, 3)
        val adapter = RedeemAdapter(mockItems) { selectedItem ->
            showConfirmDialog(selectedItem)
        }
        binding.recyclerViewRedeem.adapter = adapter
    }

    private fun showConfirmDialog(skin: Skin) {
        AlertDialog.Builder(requireContext())
            .setTitle("Confirm Redemption")
            .setMessage("Do you want to redeem ${skin.name} for ${skin.price} coins?")
            .setPositiveButton("Confirm") { dialog, _ ->
                performRedeem(skin)
                dialog.dismiss()
            }
            .setNegativeButton("Cancel") { dialog, _ ->
                dialog.dismiss()
            }
            .create()
            .show()
    }

    private fun performRedeem(skin: Skin) {
        val currentCoins = sharedViewModel.totalCoins.value ?: 0

        if (currentCoins >= skin.price) {
            val newTotal = currentCoins - skin.price
            deductCoins(newTotal, skin)
        } else {
            AlertDialog.Builder(requireContext())
                .setTitle("Redemption Failed")
                .setMessage("Insufficient coins!")
                .setPositiveButton("Confirm", null)
                .show()
        }
    }

    private fun deductCoins(newTotal:Int, skin: Skin){
        val json = JSONObject().apply{
            put("TotalCoins", newTotal)
        }

        val body = json.toString().toRequestBody("application/json; charset=utf-8".toMediaType())

        val request = okhttp3.Request.Builder()
            .url("http://10.0.2.2:5000/api/User/UpdateCoinsApi")
            .post(body)
            .build()

        client.newCall(request).enqueue(object: Callback {
            override fun onFailure(call: Call, e: IOException) {
                activity?.runOnUiThread {
                    Toast.makeText(context, "Network error", Toast.LENGTH_LONG).show()
                }
            }

            override fun onResponse(call: Call, response: Response) {
                if (response.isSuccessful) {
                    activity?.runOnUiThread {
                        sharedViewModel.updateTotalCoins(newTotal)
                        skin.isRedeemed = true
                        Toast.makeText(context, "Redeemed ${skin.name}!", Toast.LENGTH_SHORT).show()
                    }
                } else {
                    activity?.runOnUiThread {
                        Toast.makeText(context, "Error in server response", Toast.LENGTH_SHORT)
                            .show()
                    }
                }
            }
        })
    }

    override fun onDestroyView(){
        super.onDestroyView()
        _binding = null
    }

    // UpdateCoinsApi (total coins int)
    // post - pass token and updated number of coins to backend

    // make sure to record redemption date of item
}