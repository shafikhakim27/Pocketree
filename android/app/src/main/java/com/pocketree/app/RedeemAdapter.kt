package com.pocketree.app

import android.graphics.Color
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.recyclerview.widget.RecyclerView
import com.pocketree.app.databinding.ItemRedeemBinding

// Change <Skin> to <Any> for both skins and vouchers
class RedeemAdapter(
    private val items: List<Any>,
    private val onItemClick: (Any) -> Unit
) : RecyclerView.Adapter<RedeemAdapter.RedeemViewHolder>() {

    class RedeemViewHolder(val binding: ItemRedeemBinding) : RecyclerView.ViewHolder(binding.root)

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): RedeemViewHolder {
        val binding = ItemRedeemBinding.inflate(LayoutInflater.from(parent.context), parent, false)
        return RedeemViewHolder(binding)
    }

    override fun onBindViewHolder(holder: RedeemViewHolder, position: Int) {
        val item = items[position]
        holder.binding.itemPrice.visibility = View.VISIBLE
        holder.binding.itemStatus.visibility = View.GONE
        holder.binding.root.setOnClickListener(null)  // Global processing: Clear click events
        holder.binding.root.alpha = 1.0f

        if (item is Skin){
            val skin = item
            // holder.image.setImageResource(item.imageResId)
            holder.binding.itemImage.setImageResource(android.R.drawable.ic_menu_gallery)
            holder.binding.itemName.text = skin.skinName
            holder.binding.itemPrice.text = "${skin.skinPrice} coins"
            if (!skin.isRedeemed) {
                holder.binding.itemPrice.setTextColor(Color.parseColor("#4CAF50"))
            } else {
                holder.binding.itemPrice.visibility = View.GONE
                holder.binding.itemStatus.visibility = View.VISIBLE
                if (skin.isEquipped) {
                    holder.binding.itemStatus.text = "Equipped"
                    holder.binding.itemStatus.setTextColor(Color.parseColor("#4CAF50"))
                } else {
                    holder.binding.itemStatus.text = "Redeemed"
                    holder.binding.itemStatus.setTextColor(Color.BLUE)
                }
            }
            holder.binding.root.setOnClickListener { onItemClick(skin) }
        } else if (item is Voucher) {
            val voucher = item
            holder.binding.itemImage.setImageResource(R.drawable.redeem)
            holder.binding.itemName.text = voucher.voucherName
            holder.binding.itemPrice.visibility = View.GONE
            holder.binding.itemStatus.visibility = View.VISIBLE
            if (voucher.isValid && !voucher.isUsed) {
                holder.binding.itemStatus.text = "Valid"
                holder.binding.itemStatus.setTextColor(Color.parseColor("#4CAF50"))
                holder.binding.root.setOnClickListener { onItemClick(voucher) } // Set the button
            } else if (voucher.isValid) {
                holder.binding.itemStatus.text = "Used"
                holder.binding.itemStatus.setTextColor(Color.RED)
            } else {
                holder.binding.itemStatus.text = "Expired" // kiv can add in expiry date logic later on
                holder.binding.root.alpha = 0.5f
            }
        }
    }

    override fun getItemCount() = items.size
}