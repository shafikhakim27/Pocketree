// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Set up signalR.HubConnection
const connection = new signalR.HubConnectionBuilder().withUrl("/mapHub").build();

// SignalR Listener: Plant the tree at the specified place when server sends a new tree
connection.on("UpdateMapData", (data) => {
    plantTreeOnMap(data.x, data.y);
});

// Initialise the connection at the start
connection.start().then(() => {
    console.log("Connected to MapHub!");
    connection.invoke("JoinWebGroup");

    // Load existing trees from the database and fill up the map at the start of the connection
    fetch('/api/Contribution/GetAllForestTrees')
        .then(response => response.json())
        .then(trees => {
            trees.forEach(t => plantTreeOnMap(t.xCoordinate, t.yCoordinate));
        });
}).catch(err => console.error(err.toString()));

// Function to add a single tree to the overlay
function plantTreeOnMap(x, y) {
    const overlay = document.getElementById("tree-overlay");

    if (overlay) {
        // Create a new image element for the tree
        const tree = document.createElement("img");

        // Path to the tree icon
        tree.src = "/images/icons/map-tree.png";

        // CSS for absolute placement
        tree.style.position = "absolute";
        tree.style.width = "25px";  // Size of the tree to fit the map scale
        tree.style.height = "auto";

        // Subtract half the width/height (approx 12px) to center the icon on the point
        tree.style.left = `calc(${x}% - 12px)`;
        tree.style.top = `calc(${y}% - 12px)`;

        // Add a simple pop-in animation
        tree.style.transform = "scale(0)";
        tree.style.transition = "transform 0.4s ease-out";

        overlay.appendChild(tree);

        // Trigger the animation
        setTimeout(() => { tree.style.transform = "scale(1)"; }, 50);
    }
    else
    {
        console.error("Display of global forest trees failed");
    }
}
