package com.pocketree.app

import android.app.AlertDialog
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Toast
import androidx.fragment.app.Fragment
import androidx.recyclerview.widget.GridLayoutManager
import com.pocketree.app.databinding.FragmentRedeemBinding


class RedeemFragment: Fragment() {
    private var _binding: FragmentRedeemBinding? = null
    private val binding get() = _binding!!

    // Mock Data
    private val mockItems = listOf(
        RedeemItem(1, "Item1", 1, R.drawable.redeem_item_1),
        RedeemItem(2, "Item2", 2, R.drawable.redeem_item_2),
        RedeemItem(3, "Item3", 3, R.drawable.redeem_item_3),
        RedeemItem(4, "Item4", 4, R.drawable.redeem_item_4),
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
    }

    private fun setupRecyclerView() {
        // GridLayoutManager: parameter 3 indicates that 3 items are displayed in one row.
        binding.recyclerViewRedeem.layoutManager = GridLayoutManager(context, 3)
        val adapter = RedeemAdapter(mockItems) { selectedItem ->
            showConfirmDialog(selectedItem)
        }
        binding.recyclerViewRedeem.adapter = adapter
    }

    private fun showConfirmDialog(item: RedeemItem) {
        AlertDialog.Builder(requireContext())
            .setTitle("Confirm Redemption")
            .setMessage("Do you want to redeem ${item.name} for ${item.price} coins?")
            .setPositiveButton("Confirm") { dialog, _ ->
                // TODO: When the user clicks "confirm", the deduction logic is executed
                performRedeem(item)  // currently, only a prompt is displayed.
                dialog.dismiss()
            }
            .setNegativeButton("Cancel") { dialog, _ ->
                dialog.dismiss()
            }
            .create()
            .show()
    }

    private fun performRedeem(item: RedeemItem) {
        // TODO: This section will subsequently deduct gold coins from the database.
        Toast.makeText(context, "Redeemed ${item.name}!", Toast.LENGTH_SHORT).show()
        // TODO: The next step is to simulate updating the coin display on the UI.
        // binding.coinDisplay.text = "400 Coins"
    }

    override fun onDestroyView(){
        super.onDestroyView()
        _binding = null
    }
}