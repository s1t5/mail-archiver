#!/bin/bash
# ============================================
# MailArchiver Fork - Linux Server Deploy Skript
# ============================================
# 
# usage:
#   ./deploy-linux.sh          # Deploys latest image from GHCR
#   ./deploy-linux.sh local     # Deploys locally built image
#   ./deploy-linux.sh stop      # Stops all containers
#   ./deploy-linux.sh logs      # Shows application logs
#   ./deploy-linux.sh update    # Pulls latest image and redeploys
#

set -e

# Colors for output
SUCCESS="\e[92m"
ERROR="\e[91m"
INFO="\e[93m"
RESET="\e[0m"

log_success() { echo -e "$SUCCESS[$(date +'%H:%M:%S')] $1$RESET"; }
log_error() { echo -e "$ERROR[$(date +'%H:%M:%S')] $1$RESET"; }
log_info() { echo -e "$INFO[$(date +'%H:%M:%S')] $1$RESET"; }

# Configuration
IMAGE_NAME="ghcr.io/git-usr123/mailarchiver:my-fork-latest"
LOCAL_IMAGE="mailarchiver:my-fork-latest"
COMPOSE_FILE="docker-compose.ghcr.yml"
PROJECT_NAME="mailarchiver-fork"

# Check if running as root
if [ "$(id -u)" -eq 0 ]; then
    log_error "Do not run as root! Use a regular user with docker permissions."
    exit 1
fi

# Check Docker
if ! command -v docker &> /dev/null; then
    log_error "Docker is not installed!"
    log_info "Install Docker: https://docs.docker.com/engine/install/"
    exit 1
fi

# Check Docker Compose
if ! docker compose version &> /dev/null; then
    log_error "Docker Compose is not available!"
    log_info "Install Docker Compose: https://docs.docker.com/compose/install/"
    exit 1
fi

# Check docker-compose.ghcr.yml exists
if [ ! -f "$COMPOSE_FILE" ]; then
    log_error "$COMPOSE_FILE not found!"
    log_info "Make sure you have the file in the current directory."
    exit 1
fi

# Functions
deploy() {
    local image=$1
    log_info "Deploying with image: $image"
    
    # Update the compose file to use the correct image
    if [ "$image" != "$IMAGE_NAME" ]; then
        # Use local image
        sed -i "s|ghcr.io/git-usr123/mailarchiver:my-fork-latest|$image|g" $COMPOSE_FILE
    fi
    
    log_info "Starting containers..."
    docker compose -f $COMPOSE_FILE -p $PROJECT_NAME up -d
    
    log_success "Containers started!"
    log_info "Application will be available at: http://<your-server-ip>:5000"
    log_info ""
    log_info "Next steps:"
    log_info "  1. Set up a reverse proxy (nginx, Apache, Traefik) for HTTPS"
    log_info "  2. Configure firewall to allow port 5000 (or change in docker-compose)"
    log_info "  3. Access the application and configure your email accounts"
}

stop_containers() {
    log_info "Stopping containers..."
    docker compose -f $COMPOSE_FILE -p $PROJECT_NAME down
    log_success "Containers stopped!"
}

show_logs() {
    log_info "Showing application logs (Press CTRL+C to exit)..."
    docker compose -f $COMPOSE_FILE -p $PROJECT_NAME logs -f mailarchive-app
}

update_deploy() {
    log_info "Pulling latest image from GHCR..."
    docker pull $IMAGE_NAME
    stop_containers
    deploy $IMAGE_NAME
}

build_local() {
    log_info "Building local image..."
    docker build -t $LOCAL_IMAGE .
    log_success "Local image built: $LOCAL_IMAGE"
    deploy $LOCAL_IMAGE
}

# Main logic
case "$1" in
    stop)
        stop_containers
        ;;
    logs)
        show_logs
        ;;
    update)
        update_deploy
        ;;
    local)
        build_local
        ;;
    *)
        # Default: deploy from GHCR
        log_info "Deploying MailArchiver Fork from GHCR..."
        deploy $IMAGE_NAME
        ;;
esac

log_success "Done!"
