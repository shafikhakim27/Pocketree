package com.pocketree.app

import android.graphics.Color
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.ImageView
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView
import com.pocketree.app.databinding.ItemBadgeBinding
import com.pocketree.app.databinding.ItemRedeemBinding

class RedeemAdapter(
    private val skins: List<Skin>,
    private val onItemClick: (Skin) -> Unit
) : RecyclerView.Adapter<RedeemAdapter.RedeemViewHolder>() {

    class RedeemViewHolder(val binding: ItemRedeemBinding) : RecyclerView.ViewHolder(binding.root)

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): RedeemViewHolder {
        val binding = ItemRedeemBinding.inflate(LayoutInflater.from(parent.context), parent, false)
        return RedeemViewHolder(binding)
    }

    override fun onBindViewHolder(holder: RedeemViewHolder, position: Int) {
        val skin = skins[position]

        holder.binding.itemName.text = skin.name
        // 这里设置图片，暂时使用默认图标
        // holder.image.setImageResource(item.imageResId)
        holder.binding.itemImage.setImageResource(android.R.drawable.ic_menu_gallery)

        if (skin.isRedeemed) {
            holder.binding.itemPrice.text = "Redeemed" // change price to "Redeemed"
            holder.binding.itemPrice.setTextColor(Color.GRAY) // to make it look disabled
            holder.binding.root.alpha = 0.5f // to make the whole card slightly transparent (visual cue)
            holder.binding.root.setOnClickListener(null) // disable click reaction
        } else {
            // show actual price
            holder.binding.itemPrice.text = "${skin.price} coins"
            holder.binding.itemPrice.setTextColor(Color.parseColor("#4CAF50"))
            holder.binding.root.alpha = 1.0f
            holder.binding.root.setOnClickListener { onItemClick(skin) }
        }
    }

    override fun getItemCount() = skins.size
}