package com.pocketree.app

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.TextView
import androidx.recyclerview.widget.ListAdapter
import androidx.recyclerview.widget.RecyclerView

// this adapter handles the visual "checking off" of tasks
// if task is completed, the click listener is disabled and UI is changed

class TaskAdapter(
    private var taskList: List<Task>,
    private val onTaskClick: (Task) -> Unit
) : RecyclerView.Adapter<TaskAdapter.TaskViewHolder>() {

    fun updateTasks(newTasks: List<Task>) {
        this.taskList = newTasks
        notifyDataSetChanged()
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): TaskViewHolder {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_task, parent, false)
        return TaskViewHolder(view)
    }

    override fun onBindViewHolder(holder: TaskViewHolder, position: Int) {
        val task = taskList[position]

        // Bind the data to the views held by the ViewHolder
        holder.description.text = task.description
        holder.reward.text = "${task.coinReward} Coins" // Adjust this to match your Task variable name

        if (task.isCompleted) {
            holder.actionButton.text = "Completed!"
            holder.actionButton.isEnabled = false
            holder.itemView.alpha = 0.5f
        } else {
            holder.actionButton.text = if (task.requiresEvidence) "Upload a photo" else "Let's do it!"
            holder.actionButton.isEnabled = true
            holder.itemView.alpha = 1.0f
            holder.actionButton.setOnClickListener { onTaskClick(task) }
        }
    }

    override fun getItemCount() = taskList.size

    // This is the "cache" for your views. Finding them once here makes binding fast and reliable.
    class TaskViewHolder(itemView: View) : RecyclerView.ViewHolder(itemView) {
        val description: TextView = itemView.findViewById(R.id.descriptionTv)
        val reward: TextView = itemView.findViewById(R.id.rewardTv)
        val actionButton: Button = itemView.findViewById(R.id.actionBtn)
    }
}

//class TaskAdapter(
//    private var taskList: List<Task>,
//    private val onTaskClick: (Task) -> Unit
//): RecyclerView.Adapter<TaskAdapter.TaskViewHolder>() {
//
//    fun updateTasks(newTasks:List<Task>) {
//        this.taskList = newTasks
//        notifyDataSetChanged()
//    }
//
//    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): TaskViewHolder {
//        val view = LayoutInflater.from(parent.context)
//            .inflate(R.layout.item_task, parent, false)
//        return TaskViewHolder(view)
//    }
//
//    override fun onBindViewHolder(holder: TaskViewHolder, position:Int) {
//        val task = taskList[position]
//        val actionButton = holder.itemView.findViewById<Button>(R.id.actionBtn)
//        val description = holder.itemView.findViewById<TextView>(R.id.descriptionTv)
//
//        description.text = task.description
//
//        if (task.isCompleted) {
//            actionButton.text = "Completed!"
//            actionButton.isEnabled = false
//            holder.itemView.alpha = 0.5f // visual "checked off" state
//        } else {
//            actionButton.text = if (task.requiresEvidence) "Upload a photo" else "Let's do it!"
//            actionButton.isEnabled = true
//            holder.itemView.alpha = 1.0f
//            actionButton.setOnClickListener { onTaskClick(task) }
//        }
//    }
//
//    override fun getItemCount() = taskList.size
//
//    class TaskViewHolder(itemView: View): RecyclerView.ViewHolder(itemView)
//}